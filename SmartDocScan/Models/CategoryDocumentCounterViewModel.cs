using SmartDocScan.Data;
using System;
using System.ComponentModel.DataAnnotations;

namespace SmartDocScan.Models
{
    public class CategoryDocumentCounterViewModel
    {
        public int cat_id { get; set; }
        public string cat_name { get; set; }
        public int counter { get; set; }
    }
}