using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations;

namespace SmartDocScan.Models
{
    public class LoginViewModel
    {
        [Required(ErrorMessage = "User Name is required.")]
        [Display(Name = "User Name")]
        //[EmailAddress]        
        public string UserName { get; set; }

        [Required(ErrorMessage = "Password is required.")]
        [DataType(DataType.Password)]
        [Display(Name = "Password")]
        public string Password { get; set; }

        [Display(Name = "Remember me")]
        public bool RememberMe { get; set; }
    }
}