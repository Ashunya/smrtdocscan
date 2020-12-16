using System.ComponentModel.DataAnnotations;

namespace SmartDocScan.Models
{
    public class BoxViewModel
    {
        
        public int box_id { get; set; }

        [Required(ErrorMessage = "Company Id is required")]
        public int comp_id { get; set; }

        [Required(ErrorMessage = "Box Id is required")]
        public int box_ext_id { get; set; }

        [Required(ErrorMessage = "Box Name is required")]
        public string box_name { get; set; }

        [Required(ErrorMessage = "Aisle is required")]
        public string aisle { get; set; }

        [Required(ErrorMessage = "Section is required")]
        public string section { get; set; }

        [Required(ErrorMessage = "Row is required")]
        public string brow { get; set; }

        [Required(ErrorMessage = "Column is required")]
        public string bcolumn { get; set; }
    }
}