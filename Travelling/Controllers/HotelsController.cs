using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Options;
using Microsoft.VisualBasic.FileIO;
using System;
using System.ComponentModel.DataAnnotations;
using System.Data;
using System.Data.SqlClient;
using System.Diagnostics;
using System.Drawing.Printing;
using System.Net;
using System.Text.Json.Nodes;
using Travelling.Models;
using Travelling.Services;
using Travelling.Utility;
using Page = Travelling.Models.Page;

namespace Travelling.Controllers
{
    public class HotelsController : Controller
    {
        private readonly double searchRadius = 100;
        private readonly IWebHostEnvironment environment;
        private readonly int resultsOnPage = 10;

        private readonly ILogger<HomeController> logger;
        private readonly Database database;
        private readonly GoogleMapsService googleMapsService;
        private readonly HotelsService hotelsService;

        public HotelsController(ILogger<HomeController> logger, IWebHostEnvironment environment, Database database, GoogleMapsService googleMapsService, HotelsService hotelsService)
        {
            this.logger = logger;
            this.environment = environment;
            this.database = database;
            this.googleMapsService = googleMapsService;
            this.hotelsService = hotelsService;
        }

        public async Task<IActionResult> Index([FromQuery] LocationSearchArgs args, int pageNumber = 1)
        {
            ViewBag.SearchArgs = args;
            ViewBag.PageNumber = pageNumber;
            return View(args);
        }

        public async Task<IActionResult> Search([FromQuery] LocationSearchArgs args, int pageNumber = 1, SortType sortType = SortType.Distance)
        {
            (Location, string?) searchLocation = await googleMapsService.GetLocationByAddress(args.LocationAddress);

            Task<List<HousingOffer>> apiOffersTask = hotelsService.GetOffers(searchLocation.Item2);
            Task<List<HousingOffer>> databaseOffersTask = database.GetHousings();
            Task<List<HousingOffer>> googleOffersTask =
                googleMapsService.GetResponseForHotelsAroundLocation(searchLocation.Item1)
                .ContinueWith(data => ParseOffers(data.Result));

            Task.WaitAll(apiOffersTask, googleOffersTask, databaseOffersTask);

            List<HousingOffer> databaseOffers = databaseOffersTask.Result
                .Where(o => o.GetAvailableOptions(args).Any()).ToList();
            List<HousingOffer> apiOffers = apiOffersTask.Result;

            apiOffers.AddRange(googleOffersTask.Result);
            await database.Save(apiOffers);
            databaseOffers.AddRange(apiOffers);

            IEnumerable<HousingOffer> relevantOffers = databaseOffers
                .Where(offer => offer.Location.GetDistance(searchLocation.Item1) < searchRadius);

            switch (sortType)
            {
                case SortType.Distance:
                    relevantOffers = relevantOffers.OrderBy(offer => offer.Location.GetDistance(searchLocation.Item1));
                    break;

                case SortType.Price:
                    relevantOffers = relevantOffers.OrderBy(offer => offer.Price);
                    break;
            }
                

            Page page = new Page(relevantOffers.Count(), pageNumber, resultsOnPage);

            IEnumerable<HousingOffer> cutOffers = relevantOffers.Skip((pageNumber - 1) * resultsOnPage).Take(resultsOnPage);

            ViewBag.SearchArgs = args;
            ViewBag.Page = page;
            ViewBag.SortType = sortType;
            return View(cutOffers);
        }

        public async Task<IActionResult> Offer(int offerId, [FromQuery] SearchArgs args)
        {
            HousingOffer offer = (await database.GetHousings()).First(o => o.Id == offerId);


            foreach (var reservation in offer.Options.SelectMany(option => option.Reservations))
            {
                reservation.User = await database.GetUser(reservation.UserId);
            }

            ViewBag.SearchArgs = args;
            return View(offer);
        }

        public async Task<IActionResult> OfferControl(int offerId)
        {
            HousingOffer offer = (await database.GetHousings()).First(o => o.Id == offerId);

            foreach (var reservation in offer.Options.SelectMany(option => option.Reservations))
            {
                reservation.User = await database.GetUser(reservation.UserId);
            }

            return View(offer);
        }


        public async Task<IActionResult> Option(int offerId, int optionId, [FromQuery] SearchArgs args, bool isPreview = false)
        {
            HousingOffer offer = await database.GetOffer(offerId);
            HousingOption option = offer.Options.First(o => o.Id == optionId);

            ViewBag.IsPreview = isPreview;
            ViewBag.SearchArgs = args;
            return View((offer, option));
        }

        public async Task<IActionResult> OptionControl(int offerId, int optionId)
        {
            HousingOffer offer = await database.GetOffer(offerId);
            HousingOption option = offer.Options.First(o => o.Id == optionId);

            foreach (var reservation in option.Reservations)
            {
                reservation.User = await database.GetUser(reservation.UserId);
            }

            return View(option);
        }

        [Authorize]
        public async Task<IActionResult> Book(int offerId, int optionId, [FromQuery] SearchArgs args)
        {
            HousingOffer offer = await database.GetOffer(offerId);
            HousingOption option = offer.Options.First(o => o.Id == optionId);
            UserViewModel user = (await database.GetUser(User.Identity.Name));

            if (!option.IsAvailable(args))
            {
                return RedirectToAction("Error", "Home");
            }

            Reservation reservation = new Reservation()
            {
                StartDate = args.ArriveDate,
                EndDate = args.DepartureDate,
                Option = option,
                UserId = user.Id.Value
            };

            ReservationNotification notification = new ReservationNotification()
            {
                Reservation = reservation
            };

            await database.Save(reservation);
            await database.Save(notification);

            return View();
        }

        [Authorize]
        public IActionResult Create()
        {
            return View();
        }

        [Authorize]
        public async Task<IActionResult> Edit(int offerId)
        {
            UserViewModel user = await database.GetUser(User.Identity.Name);
            HousingOffer offer = await database.GetOffer(offerId);

            //if (user.Id != offer.OwnerId)
            {
                //return RedirectToAction("Error", "Home");
            }

            CreateModel model = new CreateModel()
            {
                Id = offer.Id,
                Name = offer.Name,
                Description = offer.Description,
                Location = offer.Location.Address,
                OptionModels = new List<OptionModel>(offer.Options.Count)
            };

            foreach (var option in offer.Options)
            {
                model.OptionModels.Add(new OptionModel()
                {
                    Name = option.Name,
                    Description = option.Description,
                    Beds = option.BedsAmount,
                    Meters = option.MetersAmount,
                    Price = option.Price
                });
            }

            return View(model);
        }

        [Authorize]
        [HttpPost]
        public async Task<IActionResult> Edit(CreateModel model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            HousingOffer offer = await GetOfferFromModel(model);
            await database.Update(offer);

            return RedirectToAction("Offers", "Account");
        }

        [Authorize]
        [HttpPost]
        public async Task<IActionResult> Create(CreateModel model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            HousingOffer offer = await GetOfferFromModel(model);

            await database.Save(offer);

            return RedirectToAction("Offers", "Account");
        }

        private async Task<HousingOffer> GetOfferFromModel(CreateModel model)
        {
            Task<(Location, string?)> searchLocation = googleMapsService.GetLocationByAddress(model.Location);
            List<Image> images = new List<Image>();
            Task<UserViewModel> user = database.GetUser(User.Identity.Name);

            if (model.Images != null)
            {
                foreach (var file in model.Images)
                {
                    string path = await CreateImageOnServer(file);

                    images.Add(new Image()
                    {
                        Uri = path
                    });
                }
            }

            List<HousingOption> options = new List<HousingOption>(model.OptionModels.Count);

            foreach (var optionModel in model.OptionModels)
            {
                List<Image> optionImages = new List<Image>();

                if (optionModel.Images != null)
                {
                    foreach (var file in optionModel.Images)
                    {
                        string path = await CreateImageOnServer(file);

                        optionImages.Add(new Image()
                        {
                            Uri = path
                        });
                    }
                }

                options.Add(new HousingOption()
                {
                    Name = optionModel.Name,
                    Description = optionModel.Description,
                    Price = optionModel.Price,
                    BedsAmount = optionModel.Beds,
                    MetersAmount = optionModel.Meters,
                    Images = optionImages
                });
            }

            HousingOffer offer = new HousingOffer()
            {
                Id = model.Id,
                Name = model.Name,
                Description = model.Description,
                Images = images,
                Options = options,
                Location = (await searchLocation).Item1,
                OwnerId = (await user).Id
            };

            return offer;
        }

        private async Task<string> CreateImageOnServer(IFormFile file)
        {
            string relativePath = Path.Combine("img", file.FileName);
            string path = Path.Combine(environment.WebRootPath, relativePath);

            while (System.IO.File.Exists(path))
            {
                string directory = path.Substring(0, path.LastIndexOf(Path.DirectorySeparatorChar));
                path = Path.Combine(directory, Guid.NewGuid().ToString() + Path.GetExtension(path));
            }

            using (var fileStream = new FileStream(path, FileMode.Create))
            {
                await file.CopyToAsync(fileStream);
            }

            return "/" + relativePath;
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }

        private List<HousingOffer> ParseOffers(string data)
        {
            JsonNode root = JsonObject.Parse(data);
            JsonArray results = (JsonArray)root["results"];

            List<HousingOffer> offers = new List<HousingOffer>(results.Count);
            foreach (JsonNode result in results)
            {
                string name = (string)result["name"];

                if (database.DoesOfferExists(name))
                {
                    continue;
                }

                JsonNode locationJson = result["geometry"]["location"];
                Location location = new Location()
                {
                    Address = (string)result["vicinity"],
                    Latitude = (float)locationJson["lat"],
                    Longitude = (float)locationJson["lng"]
                };

                HousingOffer offer = new HousingOffer()
                {
                    Name = name,
                    Location = location,
                    Description = "",
                    Options = new List<HousingOption>(),
                    Images = new List<Models.Image>()
                };

                if (result["photos"] != null)
                {
                    string photo_ref = (string)result["photos"][0]["photo_reference"];
                    string url = googleMapsService.GetPhotoUrl(photo_ref);

                    offer.Images.Add(new Models.Image()
                    {
                        Uri = url
                    });
                }

                Random random = new Random();

                offer.Options.Add(new HousingOption()
                {
                    Name = "Standard",
                    Description = "Our standard room",
                    Price = random.Next(20, 80),
                    BedsAmount = 2,
                    MetersAmount = random.Next(15, 20)
                });

                offers.Add(offer);
            }

            return offers;
        }
    }
}