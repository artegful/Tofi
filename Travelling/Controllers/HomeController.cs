using Microsoft.AspNetCore.Mvc;
using System.Diagnostics;
using Travelling.Models;
using Travelling.Services;
using Travelling.Utility;

namespace Travelling.Controllers
{
    public class HomeController : Controller
    {
        private readonly ILogger<HomeController> logger;
        private readonly Database database;

        public HomeController(ILogger<HomeController> logger, Database database)
        {
            this.logger = logger;
            this.database = database;
        }

        public async Task<IActionResult> Index()
        {
            await UpdateNotifications();

            return View(new LocationSearchArgs());
        }
        
        [AcceptVerbs("GET", "POST")]
        public IActionResult VerifyArrival(DateTime arriveDate, DateTime departureDate)
        {
            if (arriveDate >= departureDate)
            {
                return Json("Departure cannot be in the same day or later");
            }
            if (arriveDate < DateTime.Today)
            {
                return Json("Arrival must be at least today");
            }
            if (arriveDate >= DateTime.Today.AddMonths(3))
            {
                return Json("Can book only for 3 months in advance");
            }

            return Json(true);
        }

        [AcceptVerbs("GET", "POST")]
        public IActionResult VerifyDeparture(DateTime departureDate, DateTime arriveDate)
        {
            return VerifyArrival(arriveDate, departureDate);
        }

        public async Task<IActionResult> NotificationsRead()
        {
            if (User.Identity != null)
            {
                User user = await database.GetUser(User.Identity.Name);
                IEnumerable<Reservation> reservations = (await database.GetHousings()).Where(offer => offer.OwnerId == user.Id)
                    .SelectMany(offer => offer.Options).SelectMany(option => option.Reservations);

                database.ClearNotifications(reservations);
            }

            return Json("");
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }

        private async Task UpdateNotifications()
        {
            if (User.Identity?.Name == null)
            {
                return;
            }

            User user = (await database.GetUser(User.Identity.Name));
            IEnumerable<Reservation> reservations = (await database.GetHousings()).Where(offer => offer.OwnerId == user.Id)
                .SelectMany(offer => offer.Options).SelectMany(option => option.Reservations);

            IEnumerable<Reservation> notSeenReservations = database.FilterReservations(reservations);
            ViewBag.NotifiedReservations = notSeenReservations;

            ViewBag.IsAdmin = user.IsAdmin;
        }

    }
}