using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Text;

namespace VMWAProvision.Models
{
    public class BusinessTypes
    {
        [Key]
        public int BusinessId { get; set; }
        public string BusinessType { get; set; }
        public int Validity { get; set; }
        public bool IsCustomizable { get; set; }
    }
}
