using Microsoft.AspNetCore.Mvc;
using System.ComponentModel.DataAnnotations;

namespace Travelling.Models
{
    public class SearchArgs: IValidatableObject
    {
        [FromQuery(Name = "arriveDate")]
        [Required]
        [DataType(DataType.Date)]
        [Remote(action: "VerifyArrival", controller: "Home", AdditionalFields = nameof(DepartureDate))]
        public DateTime ArriveDate { get; set; } = DateTime.Today;

        [FromQuery(Name = "departureDate")]
        [Required]
        [DataType(DataType.Date)]
        [Remote(action: "VerifyDeparture", controller: "Home", AdditionalFields = nameof(ArriveDate))]
        public DateTime DepartureDate { get; set; } = DateTime.Today.AddDays(1);

        [FromQuery(Name = "amountOfPeople")]
        [Required]
        [Range(1, 10)]
        public int AmountOfPeople { get; set; } = 1;

        public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        {
            if (ArriveDate.Date < DateTime.Today)
            {
                yield return new ValidationResult("Arrival must be at least today");
            }
            
            if (ArriveDate.Date > DateTime.Today.AddMonths(3))
            {
                yield return new ValidationResult("Can book only for 3 months");
            }

            if (ArriveDate.Date >= DepartureDate.Date)
            {
                yield return new ValidationResult("Cannot book a departure and arrival in the same day or earlier");
            }
        }
    }
}
