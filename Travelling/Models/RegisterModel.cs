using System.ComponentModel.DataAnnotations;

namespace Travelling.Models
{
    public class RegisterModel
    {
        [Required(ErrorMessage = "No email provided")]
        [EmailAddress(ErrorMessage = "Invalid email address")]
        public string Email { get; set; }

        [Required(ErrorMessage = "No phone number provided")]
        [DataType(DataType.PhoneNumber)]
        public string Phone { get; set; }

        [Required(ErrorMessage = "No name provided")]
        public string Name { get; set; }

        [Required(ErrorMessage = "No surname provided")]
        public string Surname { get; set; }

        [Required(ErrorMessage = "No password provided")]
        [DataType(DataType.Password)]
        public string Password { get; set; }

        [DataType(DataType.Password)]
        [Compare("Password", ErrorMessage = "Passwords must match")]
        public string ConfirmPassword { get; set; }
    }
}
