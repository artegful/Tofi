namespace Travelling.Models
{
    public class Reservation
    {
        public int Id { get; set; }
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public int UserId { get; set; }
        public HousingOption Option { get; set; }
        public User User { get; set; }
        public string SessionId { get; set; }
        public bool IsVerified { get; set; }
    }
}
