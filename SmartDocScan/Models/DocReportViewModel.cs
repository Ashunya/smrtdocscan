using System;
using System.ComponentModel.DataAnnotations;

namespace SmartDocScan.Models
{
    public class DocReportViewModel
    {
        public string FirstName { get; set; }
        public string DocumentName { get; set; }
        public int NoOfPages { get; set; }
    }
}