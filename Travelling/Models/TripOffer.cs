namespace Travelling.Models
{
    public class TripOffer
    {
        public int Id { get; set; }
        public Location DepartureLocation { get; set; }
        public Location ArrivalLocation { get; set; }
        public DateTime DepartureDate { get; set; }
        public DateTime ArrivalDate { get; set; }
        public decimal? Price { get; set; }
        public TripThread TripThread { get; set; }
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
