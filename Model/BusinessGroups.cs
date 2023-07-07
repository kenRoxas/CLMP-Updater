using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Text;

namespace VMWAProvision.Models
{
    public class BusinessGroups
    {
        [Key]
        public int BusinessGroupId { get; set; }
        public int BusinessTypeId { get; set; }
        public int UserGroupId { get; set; }
        public int? ModifiedValidity { get; set; }
        public int CreatedBy { get; set; }
        public DateTime ModifiedDate { get; set; }
    }
}
