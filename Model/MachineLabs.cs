using System;
using System.ComponentModel.DataAnnotations;

namespace VMWAProvision.Models
{
    public class MachineLabs
    {
        [Key]
        public int MachineLabsId { get; set; }
        public string ResourceId { get; set; }
        public int VEProfileId { get; set; }
        public int UserId { get; set; }
        public string MachineStatus { get; set; }
        public int IsStarted { get; set; }
        public int IsDeleted { get; set; }
        public string ScheduledBy { get; set; }
        public string MachineName { get; set; }
        public string GuacDNS { get; set; }
        public DateTime DateProvision { get; set; }
        public string Username { get; set; }
        public string Password { get; set; }
        public string FQDN { get; set; }
        public int RunningBy { get; set; }
        public string VMName { get; set; }
        public string IpAddress { get; set; }
    }
    public class MachineLabsStatus
    {
        public string ResourceId { get; set; }
        public string VMName { get; set; }
        public string MachineStatus { get; set; }
        public float? TimeRemaining { get; set; }
        public int RunningBy { get; set; }
        public float? InstructorLabHours { get; set; }
        public string DateProvision { get; set; }
        public int IsStarted { get; set; }
        public int VEProfileId { get; set; }
        public int UserId { get; set; }
        public int TenantId { get; set; }
        public int VETypeID { get; set; }
        public int GroupID { get; set; }
        public int MachineLabsId { get; set; }
        public int ModifiedDateMinutes { get; set; }
        public string GuacDNS { get; set; }
    }
    public class MachineVMStatus
    {
        public string ResourceId { get; set; }
        public string VMName { get; set; }
        public string MachineStatus { get; set; }
        public double? TimeRemaining { get; set; }
        public int RunningBy { get; set; }
        public double? InstructorLabHours { get; set; }
        public DateTime DateProvision { get; set; }
        public int IsStarted { get; set; }
        public int VEProfileId { get; set; }
        public int UserId { get; set; }
        public int TenantId { get; set; }
        public int VETypeID { get; set; }
        public int GroupID { get; set; }
        public int MachineLabsId { get; set; }
        public double ModifiedDate { get; set; }
        public string GuacDNS { get; set; }
    }
    public class VMSuccess
    {
        public string ResourceId { get; set; }
        public string ComputerName { get; set; }
        public string Username { get; set; }
        public string Password { get; set; }
        public string Region { get; set; }
        public string Size { get; set; }
        public string Fqdn { get; set; }
        public string OsType { get; set; }
        public string Status { get; set; }
        public string LastStatus { get; set; }
        public string ProvisioningStatus { get; set; }
        public bool IsReadyForUsage { get; set; }
        public string DeploymentDuration { get; set; }
        public string VirtualMachineName { get; set; }
    }
    public class VMStats
    {
        public string ResourceId { get; set; }
        public string LastStatusDescription { get; set; }
        public bool IsDeleted { get; set; }
        public string ProvisioningStatus { get; set; }
        public string DeploymentDuration { get; set; }
        public string VirtualMachineName { get; set; }
        public bool IsReadyForUsage { get; set; }
    }
    public class ExpiredMachines
    {
        public string ResourceId { get; set; }
        public string VMName { get; set; }
        public string CourseName { get; set; }
        public double? TimeRemaining { get; set; }
        public double? InstructorLabHours { get; set; }
        public string DateProvision { get; set; }
        public string Email { get; set; }
        public int TenantId { get; set; }
        public int UserGroup { get; set; }
        public int VETypeID { get; set; }
        public string GroupName { get; set; }
        public string GuacDNS { get; set; }
    }
    public class MachineResources
    {
        public string Email { get; set; }
        public string ResourceId { get; set; }
        public string VMName { get; set; }
        public double? InstructorLabHours { get; set; }
        public double? TimeRemaining { get; set; }
        public double? TotalLabHours { get; set; }
        public string CourseName { get; set; }
        public string DateProvision { get; set; }
        public string GroupName { get; set; }
        public string ClientCode { get; set; }
        public int DaysRendered { get; set; }
    }

    public class LabsProvisionModel
    {
        public string VirtualMachineName { get; set; }

        public string TenantId { get; set; }

        public string Fqdn { get; set; }

        public string apiprefix { get; set; }

        public string ResourceGroupName { get; set; }

        public string location { get; set; }

        public string computerName { get; set; }

    }

    public class NotificationVM
    {
        public string ResourceId { get; set; }
        public string Email { get; set; }
        public string CourseName { get; set; }
        public string VMName { get; set; }
        public double? TimeRemaining { get; set; }
        public double? LabHoursTotal { get; set; }
        public double? InstructorRemaining { get; set; }
        public string Dateprovision { get; set; }
        public int UserGroupId { get; set; }
        public int TenantId { get; set; }
        public string ScheduledBy { get; set; }
    }
    public class VMListEmail
    {
        public string ResourceId { get; set; }
        public string Email { get; set; }
        public string CourseName { get; set; }
        public string VMName { get; set; }
        public double? TimeRemaining { get; set; }
        public double? LabHoursTotal { get; set; }
        public double? InstructorRemaining { get; set; }
        public string Dateprovision { get; set; }
        public string GroupName { get; set; }
        public string ScheduledBy { get; set; }
    }
    public class VMDeleteList
    {
        public string VMName { get; set; }
        public string ClientCode { get; set; }       
        public string ResourceGroup { get; set; }       
        public string StorageAccountName { get; set; }       
    }
    public class IpFirewall
    {
        public string VMName { get; set; }
    }

    public class IpParams
    {
        public string VMName { get; set; }
        public string DeployName { get; set; }
    }
}
