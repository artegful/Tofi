namespace Travelling.Models
{
    public class ReservationNotification
    {
        public int Id { get; set; }
        public Reservation? Reservation { get; set; }
        public ActionType ActionType { get; set; } = ActionType.Add;
    }

    public enum ActionType
    {
        Add,
        Cancel
    }
}
