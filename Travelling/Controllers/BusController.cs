using Microsoft.AspNetCore.Mvc;
using Travelling.Models;
using Travelling.Services;

namespace Travelling.Controllers
{
    public class BusController: Controller
    {
        private const int ITEMS_ON_PAGE = 10;

        private readonly Database database;
        private readonly GoogleMapsService googleMapsService;
        private readonly YandexMapsService yandexMapsService;

        public BusController(Database database, GoogleMapsService googleMapsService, YandexMapsService yandexMapsService)
        {
            this.database = database;
            this.googleMapsService = googleMapsService;
            this.yandexMapsService = yandexMapsService;
        }

        public IActionResult Index([FromQuery] BusSearchArgs args)
        {
            return View(args);
        }

        public async Task<IActionResult> Search([FromQuery] BusSearchArgs args, int pageNumber = 1, SortType sortType = SortType.Distance)
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
    }
}
