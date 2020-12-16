using SmartDocScan.Data;
using System;
using System.ComponentModel.DataAnnotations;

namespace SmartDocScan.Models
{
    public class LoginUserViewModel
    {
        public string username { get; set; }
        public string name { get; set; }
        public string password { get; set; }
        public int comp_id { get; set; }
        public byte upload_doc { get; set; }
        public byte scan_doc { get; set; }
        public byte delete_doc { get; set; }
        public byte delete_manage { get; set; }
        public byte print_doc { get; set; }
        public byte download_doc { get; set; }
        public byte add_cat { get; set; }
        public byte add_users { get; set; }
        public byte add_patients { get; set; }
        public byte box { get; set; }
        public byte report { get; set; }
        public Nullable<byte> su { get; set; }
        public byte disabled { get; set; }
        public Nullable<bool> IsAdmin { get; set; }
    }
}