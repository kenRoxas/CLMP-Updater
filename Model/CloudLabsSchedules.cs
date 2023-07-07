using System;
using System.ComponentModel.DataAnnotations;

namespace VMWAProvision.Models
{
    public class CloudLabsSchedules
    {
        [Key]
        public int CloudLabsScheduleId { get; set; }
        public int VEProfileID { get; set; }
        public int UserId { get; set; }

        public double? TimeRemaining { get; set; }
        public double? LabHoursTotal { get; set; }
        public DateTime? StartLabTriggerTime { get; set; }
        public DateTime? RenderPageTriggerTime { get; set; }

        public double? InstructorLabHours { get; set; }
        public DateTime? InstructorLastAccess { get; set; }

        public int MachineLabsId { get; set; }
        public MachineLabs MachineLabs { get; set; }

    }
}
