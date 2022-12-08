namespace Travelling.Models
{
    public class HousingOption
    {
        public int Id { get; set; }
        public string? ApiId { get; set; }
        public string Name { get; set; }
        public string? Description { get; set; }
        public List<Image> Images { get; set; } = new List<Image>();
        public decimal Price { get; set; }
        public int BedsAmount { get; set; }
        public int MetersAmount { get; set; }
        public HousingOffer Offer { get; set; }
        public List<Reservation> Reservations { get; set; } = new List<Reservation>();

        public bool IsAvailable(SearchArgs args)
        {
            return Reservations.All(res => res.StartDate >= args.DepartureDate || args.ArriveDate >= res.EndDate);
        }
    }
}
