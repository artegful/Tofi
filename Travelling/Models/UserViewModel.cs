namespace Travelling.Models
{
    public class UserViewModel
    {
        public UserViewModel()
        { }

        public UserViewModel(RegisterModel model)
        {
            Email = model.Email;
            Password = model.Password;
            Name = model.Name;
            Surname = model.Surname;
            Phone = model.Phone;
        }

        public int? Id { get; set; }

        public string Email { get; set; }

        public string Password { get; set; }

        public string Name { get; set; }

        public string Surname { get; set; }

        public string Phone { get; set; }
    }
}
