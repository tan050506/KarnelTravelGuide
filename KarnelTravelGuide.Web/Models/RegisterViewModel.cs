using System.ComponentModel.DataAnnotations;

namespace KarnelTravelGuide.Web.Models
{
    public class RegisterViewModel
    {
        [Required(ErrorMessage = "Enter Your Name")]
        public string FullName { get; set; } = string.Empty;

        [Required(ErrorMessage = "Enter Your Email")]
        [EmailAddress(ErrorMessage = "Invalid Email")]
        public string Email { get; set; } = string.Empty;

        [Required(ErrorMessage = "Enter Your Number Phone")]
        [Phone(ErrorMessage = "Invalid Number Phone")]
        public string PhoneNumber { get; set; } = string.Empty;

        public string Address { get; set;} = string.Empty;

        [Required(ErrorMessage = "Enter Your Password")]
        [DataType(DataType.Password)]
        [MinLength(6, ErrorMessage = "Password must be at least 6 characters long")]
        public string Password { get; set; } = string.Empty;

        [DataType(DataType.Password)]
        [Compare("Password", ErrorMessage = "The Password does not match.")]
        public string ConfirmPassword { get; set; } = string.Empty;
    }
}