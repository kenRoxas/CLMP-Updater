using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Text;

namespace VMWAProvision.Model
{
    public class TimeSchedules
    {
        [Key]
        public int TimeScheduleId { get; set; }
        public int MachineLabsId { get; set; }
        public int VEProfileID { get; set; }
        public int UserId { get; set; }
        public string TimeZone { get; set; }
        public DateTime StartTime { get; set; }
        public int IdleTime { get; set; }
        public bool IsEnabled { get; set; }
        public DateTime DateCreated { get; set; }
        public int ScheduledBy { get; set; }
        public DateTime DateModified { get; set; }
    }
}
