using System;
using System.ComponentModel.DataAnnotations;

namespace VMWAProvision.Models
{
    public class CloudLabsGroups
    {
        [Key]
        public int CloudLabsGroupID { get; set; }
        public string GroupName { get; set; }
        public string EdxUrl { get; set; }
        public int TenantId { get; set; }
        public string CLUrl { get; set; }
        public string ApiPrefix { get; set; }
        public Int64 SubscriptionHourCredits { get; set; }
        public Int64 SubscriptionRemainingHourCredits { get; set; }
        public string CLPrefix { get; set; }
        public string CreatedBy { get; set; }
        public DateTime DateCreated { get; set; }
    }
}
