using System;
using System.ComponentModel.DataAnnotations;

namespace SmartDocScan.Models
{
    public class PatientViewModel
    {

        
        public int patient_id { get; set; }
        public int comp_id { get; set; }
        public string pext_id { get; set; }

        [Required(ErrorMessage = "First name is required.")]
        public string first_name { get; set; }

        [Required(ErrorMessage = "Last Name is required.")]
        public string last_name { get; set; }

        public Nullable<System.DateTime> dob { get; set; }
        public string gender { get; set; }
        public string physician { get; set; }
        public string box { get; set; }
        public string ssn { get; set; }
    }
}