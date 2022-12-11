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

        private const string SECRET = "whsec_mSEunD1TNNwciTcxwbAP6JdakP45QjQK";

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

            string successUrl = (Request.IsHttps ? "https://" : "http://") + Request.Host + Url.Action("Success", "Pay");
            string failUrl = "https://" + Request.Host + Url.Action("Fail", "Pay");

            HousingOffer offer = await database.GetOffer(offerId);
            HousingOption option = offer.Options.First(o => o.Id == optionId);

            if (!option.IsAvailable(args))
            {
                throw new ArgumentException("This offer is no longer available");
            }

            ProductCreateOptions productOptions = new ProductCreateOptions()
            {
                Name = offer.Name + $"({option.Name}) from {args.ArriveDate.ToString("d")} to {args.DepartureDate.ToString("d")}"
            };
            Product product = await productService.CreateAsync(productOptions);

            PriceCreateOptions priceOptions = new PriceCreateOptions()
            {
                Product = product.Id,
                UnitAmount = (long)(option.BookingPrice * 100M),
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

            Session session = null;

            session = sessionService.Create(options);

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
                throw new AccessViolationException("Current user is not authorized to cancel this offer");
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
                throw new AccessViolationException("Current user is not authorized to delete this offer");
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

                Session session = stripeEvent.Data.Object as Session;

                switch (stripeEvent.Type)
                {
                    case Events.CheckoutSessionCompleted:
                        if (session.PaymentStatus == "paid")
                        {
                            await ProcessVerifiedReservation(session);
                        }
                        break;

                    case Events.CheckoutSessionAsyncPaymentSucceeded:
                        await ProcessVerifiedReservation(session);
                        break;

                    case Events.CheckoutSessionExpired:
                    case Events.CheckoutSessionAsyncPaymentFailed:
                        await RemoveExpiredReservation(session);
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

        private async Task RemoveExpiredReservation(Session session)
        {
            Reservation? reservation = database.GetHousings().Result.SelectMany(h => h.Options)
              .SelectMany(o => o.Reservations).FirstOrDefault(r => r.SessionId == session.Id);

            if (reservation == null)
            {
                return;
            }

            await database.Delete(reservation);
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
