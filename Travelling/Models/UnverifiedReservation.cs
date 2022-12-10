namespace Travelling.Models
{
    public class UnverifiedReservation
    {
        public string SessionId { get; set; }
        public int HousingOptionId { get; set; }
        public int OwnerId { get; set; }
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
    }
}
