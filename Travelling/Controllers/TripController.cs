using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Reflection.Metadata.Ecma335;
using Travelling.Models;
using Travelling.Services;

namespace Travelling.Controllers
{
    public class TripController : Controller
    {
        private const int ITEMS_ON_PAGE = 10;

        private readonly Database database;
        private readonly GoogleMapsService googleMapsService;
        private readonly YandexMapsService yandexMapsService;

        public TripController(Database database, GoogleMapsService googleMapsService, YandexMapsService yandexMapsService)
        {
            this.database = database;
            this.googleMapsService = googleMapsService;
            this.yandexMapsService = yandexMapsService;
        }

        public IActionResult Index([FromQuery] TripSearchArgs args)
        {
            return View(args);
        }

        public async Task<IActionResult> Search([FromQuery] TripSearchArgs args, int pageNumber = 1, SortType sortType = SortType.Distance)
        {
            IEnumerable<TripOffer> offers = new List<TripOffer>();

            Task<(Location, string?)> departureLocationTask = googleMapsService.GetLocationByAddress(args.DepartureLocation);
            Task<(Location, string?)> arrivalLocationTask = googleMapsService.GetLocationByAddress(args.ArriveLocation);

            await Task.WhenAll(departureLocationTask, arrivalLocationTask);

            try
            {
                Task<Location> departureClosestSettlementTask = yandexMapsService.GetClosestSettlement(departureLocationTask.Result.Item1);
                Task<Location> arrivalClosestSettlementTask = yandexMapsService.GetClosestSettlement(arrivalLocationTask.Result.Item1);

                await Task.WhenAll(departureClosestSettlementTask, arrivalClosestSettlementTask);

                offers = (await yandexMapsService.GetTripOffers(departureClosestSettlementTask.Result,
                    arrivalClosestSettlementTask.Result, args.DepartureDate)).ToList();

                await database.Save(offers);

                switch (sortType)
                {
                    case SortType.Time:
                        offers = offers.OrderBy(offer => offer.ArrivalDate);
                        break;

                    case SortType.Price:
                        offers = offers.OrderBy(offer => offer.Price);
                        break;
                }


                Page page = new Page(offers.Count(), pageNumber, ITEMS_ON_PAGE);
                ViewBag.Page = page;
                ViewBag.PageNumber = pageNumber;
                ViewBag.SortType = sortType;

                offers = offers.Skip((pageNumber - 1) * ITEMS_ON_PAGE).Take(ITEMS_ON_PAGE);

            }
            catch (Exception ex)
            {
                Console.Write(ex.Message);
            }

            return View(offers);
        }

        public async Task<IActionResult> Trip(int id, bool isPreview = false)
        {
            TripOffer offer = await database.GetTripOffer(id);
            ViewBag.DocumentTypes = await database.GetDocumentTypes();

            if (User.Identity?.Name != null)
            {
                User user = await database.GetUser(User.Identity.Name);
                ViewBag.User = user;
            }

            ViewBag.IsPreview = isPreview || offer.DepartureDate < DateTime.Now;
            return View(offer);
        }

        [HttpPost]
        [Authorize]
        public async Task<IActionResult> Trip(int id, PassengersModel model)
        {
            if (ModelState.IsValid)
            {
                TripOffer offer = await database.GetTripOffer(id);
                User user = await database.GetUser(User.Identity.Name);

                if (offer.DepartureDate < DateTime.Now)
                {
                    return RedirectToAction("Error", "Home");
                }

                TripReservation reservation = new TripReservation()
                {
                    TripOffer = offer,
                    Owner = user,
                    Passengers = model.Passengers
                };

                await database.Save(reservation);

                return RedirectToAction("Reservations", "Account");
            }

            ViewBag.PassengersModel = model;
            return await Trip(id);
        }

        [Authorize]
        public async Task<IActionResult> Cancel(int reservationId)
        {
            User user = await database.GetUser(User.Identity.Name);
            TripReservation reservation = await database.GetTripReservation(reservationId);

            if (user.Id != reservation.Owner.Id)
            {
                return RedirectToAction("Error", "Home");
            }

            await database.EraseTripReservation(reservationId);

            return RedirectToAction("Reservations", "Account");
        }
    }
}
