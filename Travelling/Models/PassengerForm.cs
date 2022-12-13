using System.ComponentModel.DataAnnotations;

namespace Travelling.Models
{
    public class Passenger
    {
        public int Id { get; set; }
        [Required]
        public string Name { get; set; }
        [Required]
        public string Surname { get; set; }
        [Required]
        public string MiddleName { get; set; }
        [Required]
        public DateTime BirthDate { get; set; } = DateTime.Today;
        [Required]
        public int DocumentType { get; set; }
        [Required]
        public string DocumentCode { get; set; }
    }
}
