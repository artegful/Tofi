using Microsoft.AspNetCore.Mvc;
using System.ComponentModel.DataAnnotations;

namespace Travelling.Models
{
    public class TripSearchArgs
    {
        public string DepartureLocation { get; set; } = "Minsk, Belarus";

        public string ArriveLocation { get; set; } = "Moscow, Russia";

        public DateTime DepartureDate { get; set; } = DateTime.Today;
    }
}
