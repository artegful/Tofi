using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using Travelling.Models;
using Travelling.Services;
using Microsoft.AspNetCore.Authorization;
using System;
using System.Data;

namespace Travelling.Controllers
{
    public class AccountController: Controller
    {
        private readonly Database database;

        public AccountController(Database database)
        {
            this.database = database;
        }

        [HttpGet]
        public IActionResult Login()
        {
            ViewBag.ReturnUrl = Request.Headers.Referer;

            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(LoginModel model, string returnUrl)
        {
            if (ModelState.IsValid)
            {
                User user = await database.GetUser(model.Email);
                if (user != null)
                {
                    await Authenticate(model.Email);

                    return Redirect(returnUrl);
                }

                ModelState.AddModelError("", "Invalid email or password");
            }

            ViewBag.ReturnUrl = returnUrl;
            return View(model);
        }

        [HttpGet]
        public IActionResult Register()
        {
            ViewBag.ReturnUrl = Request.Headers.Referer;

            return View();
        }


        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Register(RegisterModel model, string returnUrl)
        {
            if (ModelState.IsValid)
            {
                User user = await database.GetUser(model.Email);

                if (user == null)
                {
                    User userViewModel = new User(model);

                    await database.Save(userViewModel);
                    await Authenticate(model.Email);

                    return Redirect(returnUrl);
                }
                else
                {
                    ModelState.AddModelError("", "Invalid email or password");
                }
            }

            ViewBag.ReturnUrl = returnUrl;
            return View(model);
        }

        [Authorize]
        public async Task<IActionResult> ManageUsers()
        {
            User user = await database.GetUser(User.Identity.Name);

            if (!user.IsAdmin)
            {
                throw new AccessViolationException("User is not admin");
            }

            return View((await database.GetUsers()).Where(user => !user.IsAdmin));
        }

        [Authorize]
        public async Task<IActionResult> Delete(int userId)
        {
            User user = await database.GetUser(User.Identity.Name);
            User deletedUser = await database.GetUser(userId);

            if (!user.IsAdmin || deletedUser.IsAdmin)
            {
                throw new AccessViolationException("User is not admin");
            }

            await database.Delete(deletedUser);

            return RedirectToAction("ManageUsers", "Account");
        }

        private async Task Authenticate(string userName)
        {
            var claims = new List<Claim>
            {
                new Claim(ClaimsIdentity.DefaultNameClaimType, userName)
            };

            ClaimsIdentity id = new ClaimsIdentity(claims, "ApplicationCookie", ClaimsIdentity.DefaultNameClaimType, ClaimsIdentity.DefaultRoleClaimType);
            
            await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(id));
        }

        public async Task<IActionResult> Logout()
        {
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);

            return Redirect(Request.Headers.Referer);
        }

        [Authorize]
        public async Task<IActionResult> Reservations()
        {
            User user = (await database.GetUser(User.Identity.Name));
            IEnumerable<Reservation> reservations = (await database.GetHousings())
                .SelectMany(offer => offer.Options).SelectMany(option => option.VerifiedReservations)
                .Where(reservation => reservation.UserId == user.Id && reservation.StartDate >= DateTime.Today);

            IEnumerable<TripReservation> tripReservations = await database.GetTripReservations(user.Id.Value);
            ViewBag.DocumentTypesDict = await database.GetDocumentDict();

            return View((reservations, tripReservations));
        }

        [Authorize]
        public async Task<IActionResult> Offers()
        {
            User user = (await database.GetUser(User.Identity.Name));

            IEnumerable<HousingOffer> postedOffers = (await database.GetHousings())
                .Where(offer => offer.OwnerId == user.Id);

            foreach (var reservation in postedOffers.SelectMany(offer => offer.Options).SelectMany(option => option.Reservations))
            {
                reservation.User = await database.GetUser(reservation.UserId);
            }

            return View(postedOffers);
        }
    }
}
