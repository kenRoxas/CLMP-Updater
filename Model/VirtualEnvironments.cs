using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace VMWAProvision.Models
{
    public class VirtualEnvironments
    {
        [Key]
        public int VirtualEnvironmentID { get; set; }

        [ForeignKey("VEType")]
        public int VETypeID { get; set; }
        public VETypes VEType { get; set; }

        [ForeignKey("CloudProvider")]
        [DefaultValue(1)]
        public int CloudProviderID { get; set; }
        public CloudProviders CloudProvider { get; set; }

        [Required]
        public string Title { get; set; }
        public string Description { get; set; }
    }
    public class VETypes
    {
        [Key]
        public int VETypeID { get; set; }
        [Required]
        public string Name { get; set; }
        public string Description { get; set; }
        public bool Enabled { get; set; }
        public string ThumbnailUrl { get; set; }
    }
    public class CloudProviders
    {
        [Key]
        public int CloudProviderID { get; set; }
        [Required]
        public string Name { get; set; }
    }
}
