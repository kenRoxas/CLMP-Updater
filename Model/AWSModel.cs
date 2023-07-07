using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Text;

namespace VMWAProvision.Models
{
    public class AWSJson
    {
        public string account_id { get; set; }
        public EC2 ec2_details { get; set; }
        public string region { get; set; }
        public string root { get; set; }

    }
    public class EC2
    {
        public string InstanceType { get; set; }
        public int MaxCount { get; set; }
        public int MinCount { get; set; }
        public string ImageId { get; set; }
        public string KeyName { get; set; }
        public string[] SecurityGroupIds { get; set; }
        public List<TagSpecifications> TagSpecifications { get; set; }

    }

    public class TagSpecifications
    {
        public string ResourceType { get; set; }
        public List<Tags> Tags { get; set; }
    }
    public class Tags
    {
        public string Key { get; set; }
        public string Value { get; set; }
    }

    public class AWSSize
    {
        [Key]
        public int Id { get; set; }
        public string Size { get; set; }
    }

    public class AWSData
    {
        public int VEProfileId { get; set; }
        public int UserId { get; set; }
        public string MachineSize { get; set; }
        public string SchedBy { get; set; }
        public int TenantId { get; set; }
        public int VETypeID { get; set; }
    }
    public class HeartBeatAWS
    {
        public string student_id { get; set; }
        public string instance_id { get; set; }
        public int minutes_rendered { get; set; }
    }
}
