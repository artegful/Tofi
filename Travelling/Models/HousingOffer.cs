using System.Diagnostics.Eventing.Reader;

namespace Travelling.Models
{
    public class HousingOffer
    {
        public int? Id { get; set; }
        public int LocationId { get; set; }
        public long? ApiId { get; set; }
        public Location Location { get; set; }
        public string Name { get; set; }
        public string? Description { get; set; }
        public List<Image> Images { get; set; }
        public List<HousingOption> Options { get; set; }
        public int? OwnerId { get; set; }

        public IEnumerable<HousingOption> GetAvailableOptions(SearchArgs args)
        {
            return Options.Where(option => option.IsAvailable(args));
        }

        public decimal Price => Options.Select(option => option.Price).Min();
    }
}
