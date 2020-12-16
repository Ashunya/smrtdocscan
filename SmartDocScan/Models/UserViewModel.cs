using SmartDocScan.Data;
using System;
using System.ComponentModel.DataAnnotations;

namespace SmartDocScan.Models
{
    public class UserViewModel
    {
        [Required(ErrorMessage = "Username is required.")]
        public string username { get; set; }

        [Required(ErrorMessage = "Name is required.")]
        public string name { get; set; }
        [Required(ErrorMessage = "Password is required.")]
        public string password { get; set; }

        [Required(ErrorMessage = "Company is required.")]
        public int comp_id { get; set; }

        public bool upload_doc { get; set; } 
        public bool scan_doc { get; set; } 
        public bool delete_doc { get; set; } 
        public bool delete_manage { get; set; } 
        public bool print_doc { get; set; } 
        public bool download_doc { get; set; }
        public bool add_cat { get; set; } 
        public bool add_users { get; set; }
        public bool add_patients { get; set; }
        public bool box { get; set; } 
        public bool report { get; set; }
        public bool su { get; set; }
        public bool disabled { get; set; } 
        public bool IsAdmin { get; set; }

        public int cat_comp_id { get; set; }
        public int cat_id { get; set; }
        public string cat_name { get; set; }
        public string access { get; set; }
    }
}