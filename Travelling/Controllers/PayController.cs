using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Stripe;
using Stripe.Checkout;
using System.Runtime.CompilerServices;
using Travelling.Models;
using Travelling.Services;

namespace Travelling.Controllers
{
    public class PayController : Controller
    {
        private readonly Database database;

        private readonly ProductService productService;
        private readonly PriceService priceService;
        private readonly SessionService sessionService;
        private readonly RefundService refundSerevice;
        private readonly PaymentIntentService paymentIntentService;

        private readonly object Lock = new object();

        private const string SECRET = "whsec_3c2274e8850de40f68647065530e99d2ec169c1d1547e0bb767d63b49c90b79d";

        public PayController(Database database)
        {
            this.database = database;

            productService = new ProductService();
            priceService = new PriceService();
            sessionService = new SessionService();
            refundSerevice = new RefundService();
            paymentIntentService = new PaymentIntentService();
        }

        [Authorize]
        public async Task<ActionResult> Checkout(int offerId, int optionId, [FromQuery] SearchArgs args)
        {
            string dateFormat = "yyyyMMdd";

            string successUrl = "https://" + Request.Host + Url.Action("Success", "Pay");
            string failUrl = "https://" + Request.Host + Url.Action("Fail", "Pay");

            HousingOffer offer = await database.GetOffer(offerId);
            HousingOption option = offer.Options.First(o => o.Id == optionId);

            if (!option.IsAvailable(args))
            {
                return RedirectToAction("Error", "Home");
            }

            ProductCreateOptions productOptions = new ProductCreateOptions()
            {
                Name = offer.Name + $"({option.Name}) from {args.ArriveDate.ToString("d")} to {args.DepartureDate.ToString("d")}"
            };
            Product product = await productService.CreateAsync(productOptions);

            PriceCreateOptions priceOptions = new PriceCreateOptions()
            {
                Product = product.Id,
                UnitAmount = (long)option.BookingPrice * 100,
                Currency = "USD"
            };
            Price price = await priceService.CreateAsync(priceOptions);

            var options = new SessionCreateOptions
            {
                LineItems = new List<SessionLineItemOptions>
                {
                    new SessionLineItemOptions
                    {
                        Price = price.Id,
                        Quantity = 1,
                    },
                },
                Mode = "payment",
                SuccessUrl = successUrl,
                CancelUrl = failUrl,
                CustomerEmail = User.Identity.Name
            };

            Session session = sessionService.Create(options);

            Reservation reservation = new Reservation()
            {
                SessionId = session.Id,
                UserId = (await database.GetUser(User.Identity.Name)).Id.Value,
                Option = (await database.GetHousings()).SelectMany(h => h.Options).FirstOrDefault(o => o.Id == optionId),
                StartDate = args.ArriveDate,
                EndDate = args.DepartureDate,
                IsVerified = false
            };

            await database.Save(reservation);

            Response.Headers.Add("Location", session.Url);
            return new StatusCodeResult(303);
        }

        [Authorize]
        public async Task<IActionResult> VerifyCancel(int reservationId)
        {
            Reservation? reservation = (await database.GetHousings()).SelectMany(housing => housing.Options)
                .SelectMany(option => option.Reservations)
                .FirstOrDefault(res => res.Id == reservationId);

            return View(reservation);
        }

        [HttpPost]
        [Authorize]
        public async Task<IActionResult> Cancel(int reservationId)
        {
            Reservation? reservation = (await database.GetHousings()).SelectMany(housing => housing.Options)
                .SelectMany(option => option.Reservations)
                .FirstOrDefault(res => res.Id == reservationId);

            if (reservation.UserId != (await database.GetUser(User.Identity.Name)).Id)
            {
                return RedirectToAction("Error", "Home");
            }

            if (reservation.SessionId != null)
            {
                await Refund(reservation.SessionId);
            }

            await database.Delete(reservation);

            return RedirectToAction("Reservations", "Account");
        }


        [Authorize]
        public async Task<IActionResult> Delete(int offerId)
        {
            User user = (await database.GetUser(User.Identity.Name));

            HousingOffer? offer = (await database.GetHousings())
                .FirstOrDefault(offer => offer.Id == offerId);

            if (offer.OwnerId != user.Id)
            {
                return RedirectToAction("Error", "Home");
            }

            foreach (var reservation in offer.Options.SelectMany(o => o.VerifiedReservations).Where(r => r.StartDate < DateTime.Now && r.SessionId != null))
            {
                await Refund(reservation.SessionId);
            }

            await database.Delete(offer);
            return RedirectToAction("Offers", "Account");
        }

        private async Task Refund(string sessionId)
        {
            Session session = await sessionService.GetAsync(sessionId);
            PaymentIntent paymentIntent = await paymentIntentService.GetAsync(session.PaymentIntentId);

            var options = new RefundCreateOptions
            {
                Charge = paymentIntent.LatestChargeId
            };

            refundSerevice.Create(options);
        }

        public async Task<IActionResult> Handler()
        {
            var json = await new StreamReader(HttpContext.Request.Body).ReadToEndAsync();

            try
            {
                var stripeEvent = EventUtility.ConstructEvent(json,
                  Request.Headers["Stripe-Signature"],
                  SECRET);

                Session session;

                if (stripeEvent.Type == Events.CheckoutSessionCompleted)
                {
                    session = stripeEvent.Data.Object as Session;

                    ProcessVerifiedReservation(session);
                }

                switch (stripeEvent.Type)
                {
                    case Events.CheckoutSessionCompleted:
                        session = stripeEvent.Data.Object as Session;

                        if (session.PaymentStatus == "paid")
                        {
                            await ProcessVerifiedReservation(session);
                        }

                        break;

                    case Events.CheckoutSessionAsyncPaymentSucceeded:
                        session = stripeEvent.Data.Object as Session;

                        await ProcessVerifiedReservation(session);
                        break;

                    case Events.CheckoutSessionAsyncPaymentFailed:
                        session = stripeEvent.Data.Object as Session;

                        break;
                }

                return Ok();
            }
            catch (StripeException e)
            {
                return BadRequest();
            }
        }

        private async Task ProcessVerifiedReservation(Session session)
        {
            lock(Lock)
            {
                Reservation? reservation = database.GetHousings().Result.SelectMany(h => h.Options)
               .SelectMany(o => o.Reservations).FirstOrDefault(r => r.SessionId == session.Id);

                if (reservation == null || reservation.IsVerified)
                {
                    return;
                }

                reservation.IsVerified = true;
                ReservationNotification notification = new ReservationNotification()
                {
                    Reservation = reservation
                };

                database.Delete(reservation).Wait();
                database.Save(reservation).Wait();

                database.Save(notification).Wait();
            }
        }

        public IActionResult Success()
        {
            return View();
        }

        public IActionResult Fail()
        {
            return View();
        }
    }
}
