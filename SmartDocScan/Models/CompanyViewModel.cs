using System.ComponentModel.DataAnnotations;

namespace SmartDocScan.Models
{
    public class CompanyViewModel
    {

        public int comp_id { get; set; }

        [Required(ErrorMessage = "Company Name is required.")]
        public string comp_name { get; set; }
        public string owner { get; set; }
        public string address { get; set; }
        public string location { get; set; }
        public string phone { get; set; }
        public byte barcode { get; set; }
        public byte inactive { get; set; }
    }
}