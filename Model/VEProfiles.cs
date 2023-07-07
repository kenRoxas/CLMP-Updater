using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace VMWAProvision.Models
{
    public class VEProfiles
    {
        [Key]
        public int VEProfileID { get; set; }

        [ForeignKey("VirtualEnvironment")]
        public int VirtualEnvironmentID { get; set; }
        public VirtualEnvironments VirtualEnvironment { get; set; }

        public string Name { get; set; }
        public string Description { get; set; }
        public string ThumbnailURL { get; set; }
        public DateTime DateCreated { get; set; }
    }
}
