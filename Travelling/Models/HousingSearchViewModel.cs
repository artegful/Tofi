namespace Travelling.Models
{
    public class HousingSearchViewModel
    {
        public string? Location { get; set; }
        public DateTime ArriveDate { get; set; }
        public DateTime DepartureDate { get; set; }
        public int AmountOfPeople { get; set; } = 0;
    }
}