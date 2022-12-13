using System.ComponentModel.DataAnnotations;

namespace Travelling.Models
{
    public class LoginModel
    {
        [Required(ErrorMessage = "No email provided")]
        public string Email { get; set; }

        [Required(ErrorMessage = "No password provided")]
        [DataType(DataType.Password)]
        public string Password { get; set; }
    }
}
