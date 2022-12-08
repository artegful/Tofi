using System.ComponentModel.DataAnnotations;

namespace Travelling.Models
{
    public class CreateModel
    {
        public int? Id { get; set; }
        public string Name { get; set; }
        public string Location { get; set; }
        public string? Description { get; set; }
        public List<IFormFile>? Images { get; set; }
        public List<OptionModel> OptionModels { get; set; } = new List<OptionModel>();
    }

    public class OptionModel
    {
        public string Name { get; set; }
        public string? Description { get; set; }
        public decimal Price { get; set; }
        public int Meters { get; set; }
        public int Beds { get; set; }
        public List<IFormFile>? Images { get; set; }

    }
}
