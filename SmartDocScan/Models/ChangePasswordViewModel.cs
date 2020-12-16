using SmartDocScan.Data;
using System;
using System.ComponentModel.DataAnnotations;

namespace SmartDocScan.Models
{
    public class ChangePasswordViewModel
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "Password is required.")]
        [DataType(DataType.Password)]
        public string oldPassword { get; set; }

        [Required(ErrorMessage = "New password is required.")]
        [DataType(DataType.Password)]
        public string NewPassword { get; set; }

        [Required(ErrorMessage = "Confirm password is required.")]
        [DataType(DataType.Password)]
        [Compare("NewPassword", ErrorMessage = "The new password and confirmation password not match.")]
        public string ConfirmPassword { get; set; }
    }
}