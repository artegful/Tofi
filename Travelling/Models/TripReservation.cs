namespace Travelling.Models
{
    public class TripReservation
    {
        public int Id { get; set; }
        public TripOffer TripOffer { get; set; }
        public User Owner { get; set; }
        public List<Passenger> Passengers { get; set; }
    }
}
