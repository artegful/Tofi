using Microsoft.AspNetCore.Mvc;
using System.ComponentModel.DataAnnotations;

namespace Travelling.Models
{
    public class BusSearchArgs
    {
        public string DepartureLocation { get; set; }

        public string ArriveLocation { get; set; }

        public DateTime DepartureDate { get; set; } = DateTime.Today;
    }
}
