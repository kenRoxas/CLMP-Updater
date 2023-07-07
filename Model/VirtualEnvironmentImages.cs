using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace VMWAProvision.Models
{
    public class VirtualEnvironmentImages
    {
        [Key]
        public int VirtualEnvironmentImagesID { get; set; }
        [ForeignKey("VirtualEnvironment")]
        public int VirtualEnvironmentID { get; set; }
        public VirtualEnvironments VirtualEnvironment { get; set; }
        [Required]
        public string Name { get; set; }
        public int? GroupId { get; set; }
    }
}
