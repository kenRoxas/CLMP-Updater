using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Text;

namespace VMWAProvision.Models
{
    public class VMStatus
    {
        [Key]
        public int VMStatusId { get; set; }
        public int Id { get; set; }
        public string Status { get; set; }
    }
}
