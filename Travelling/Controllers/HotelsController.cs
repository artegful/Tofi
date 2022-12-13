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

        public async Task<IActionResult> Search([FromQuery] LocationSearchArgs args, int pageNumber = 1, SortType sortType = SortType.Distance, bool searchOnlyPosted = false)
        {
            IEnumerable<HousingOffer> relevantOffers;
            (Location, string?) searchLocation;

            try
            {
                searchLocation = await googleMapsService.GetLocationByAddress(args.LocationAddress);

                if (searchOnlyPosted)
                {
                    relevantOffers = (await database.GetHousings()).Where(o => o.OwnerId != null)
                        .Where(offer => offer.Location.GetDistance(searchLocation.Item1) < searchRadius);
                }
                else
                {
                    relevantOffers = await GetFromApis(args, searchLocation);
                }
            }
            catch (AggregateException e)
            {
                return Content("An error occured during Search");
            }


            switch (sortType)
            {
                case SortType.Distance:
                    relevantOffers = relevantOffers.OrderBy(offer => offer.Location.GetDistance(searchLocation.Item1));
                    break;

                case SortType.Price:
                    relevantOffers = relevantOffers.OrderBy(offer => offer.Price);
                    break;

                case SortType.Rating:
                    relevantOffers = relevantOffers.OrderBy(offer => offer.Rating);
                    break;
            }
                
            Page page = new Page(relevantOffers.Count(), pageNumber, resultsOnPage);

            IEnumerable<HousingOffer> cutOffers = relevantOffers.Skip((pageNumber - 1) * resultsOnPage).Take(resultsOnPage);

            ViewBag.SearchArgs = args;
            ViewBag.Page = page;
            ViewBag.PageNumber = pageNumber - 1;
            ViewBag.SortType = sortType;
            ViewBag.SearchOnlyPosted = searchOnlyPosted;

            return View(cutOffers);
        }

        private async Task<IEnumerable<HousingOffer>> GetFromApis(LocationSearchArgs args, (Location, string) searchLocation)
        {
            bool isCached = await database.IsCityCached(searchLocation.Item2);

            Task<List<HousingOffer>> apiOffersTask = isCached ?
                Task.FromResult<List<HousingOffer>>(new List<HousingOffer>()) :
                hotelsService.GetOffers(searchLocation.Item2);

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

            if (!isCached)
            {
                database.CacheCity(searchLocation.Item2);
            }

            databaseOffers.AddRange(apiOffers);
            return databaseOffers.Where(offer => offer.Location.GetDistance(searchLocation.Item1) < searchRadius);
        }

        public async Task<IActionResult> Offer(int offerId, [FromQuery] SearchArgs args)
        {
            HousingOffer offer = (await database.GetHousings()).First(o => o.Id == offerId);

            if (User.Identity?.Name != null)
            {
                User user = await database.GetUser(User.Identity.Name);

                if (user.Id == offer.OwnerId)
                {
                    return RedirectToAction("OfferControl", "Hotels", new { offerId });
                }
            }

            if (offer.GoogleId != null)
            {
                offer.Comments = await googleMapsService.GetComments(offer.GoogleId);
            }

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

            if (User.Identity?.Name != null)
            {
                User user = await database.GetUser(User.Identity.Name);

                if (user.Id == offer.OwnerId)
                {
                    return RedirectToAction("OptionControl", "Hotels", new { offerId, optionId });
                }
            }

            option.Offer = offer;
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
            User user = (await database.GetUser(User.Identity.Name));

            if (!option.IsAvailable(args))
            {
                throw new ArgumentException("Trying to book a option, that is no longer available");
            }

            if (option.Price != 0)
            {
                return RedirectToAction("Checkout", "Pay",
                    new
                    {
                        offerId,
                        optionId,
                        arriveDate = args.ArriveDate,
                        departureDate = args.DepartureDate,
                        amountOfPeople = args.AmountOfPeople
                    });
            }

            Reservation reservation = new Reservation()
            {
                StartDate = args.ArriveDate,
                EndDate = args.DepartureDate,
                Option = option,
                UserId = user.Id.Value,
                IsVerified = true
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
            User user = await database.GetUser(User.Identity.Name);
            HousingOffer offer = await database.GetOffer(offerId);

            if (user.Id != offer.OwnerId)
            {
                throw new AccessViolationException("Current user is not authorized to edit this offer");
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
            Task<User> user = database.GetUser(User.Identity.Name);

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
                    Images = new List<Models.Image>(),
                    Rating = result["rating"] != null ? (float)result["rating"] : null,
                    GoogleId = (string)result["place_id"]
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

                offer.Options.Add(database.Images());

                offers.Add(offer);
            }

            return offers;
        }
    }
}