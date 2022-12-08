namespace Travelling.Models
{
    public class TripOffer
    {
        public string Name { get; set; }
        public Location DepartureLocation { get; set; }
        public Location ArrivalLocation { get; set; }
        public DateTime DepartureDate { get; set; }
        public DateTime ArrivalDate { get; set; }
        public TripType TripType { get; set; }
        public decimal? Price { get; set; }
    }

    public enum TripType
    {
        Train,
        Bus,
        Plane,
        Suburban,
        Water,
        Helicopter
    }
}
