using System;
using System.ComponentModel.DataAnnotations;

namespace VMWAProvision.Models
{
    public class CourseGrants
    {
        [Key]
        public int AccessID { get; set; }
        public int UserID { get; set; }
        public int VEProfileID { get; set; }
        public int VEType { get; set; }
        public bool IsCourseGranted { get; set; }
        public int? GrantedBy { get; set; }
    }

}
