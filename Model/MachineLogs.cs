using System;
using System.ComponentModel.DataAnnotations;

namespace VMWAProvision.Models
{
    public class MachineLogs
    {
        [Key]
        public int MachineLogsId { get; set; }
        public string ResourceId { get; set; }
        public string RequestId { get; set; }
        public string LastStatus { get; set; }
        public string Logs { get; set; }
        public DateTime? ModifiedDate { get; set; }
    }
}
