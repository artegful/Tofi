using Microsoft.AspNetCore.Mvc;
using System.ComponentModel.DataAnnotations;

namespace Travelling.Models
{
    public class LocationSearchArgs : SearchArgs
    {
        [FromQuery(Name = "locationAddress")]
        [Required(ErrorMessage = "Location is required")]
        [MinLength(3, ErrorMessage = "Address must be at least 3 characters long")]
        public string LocationAddress { get; set; } = "Minsk, Belarus";
    }
}
