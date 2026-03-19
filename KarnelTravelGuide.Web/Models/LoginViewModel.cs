using System.ComponentModel.DataAnnotations;

namespace KarnelTravelGuide.Web.Models {
    public class LoginViewModel {
        [Required(ErrorMessage = "Enter Your Email")]
        [EmailAddress(ErrorMessage = "Invalid Email")]
        public string Email { get; set;} = string.Empty;

        [Required(ErrorMessage = "Enter Your Password")]
        [DataType(DataType.Password)]
        public string Password { get; set;} = string.Empty;

        public bool RememberMe { get; set;}
    }
}