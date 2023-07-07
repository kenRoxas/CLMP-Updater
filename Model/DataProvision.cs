using Microsoft.Azure.Management.Compute.Fluent.Models;
using System;
using System.Collections.Generic;
using System.Text;
using static VMWAProvision.Helpers.Helper;

namespace VMWAProvision.Models
{    
    public class ProvisionDetails
    {
        private string machineName;
        public string CLPrefix;
        public string ResourceId
        {
            get { return Guid.NewGuid().ToString(); }
        }
        public string FQDN { get; set; }
        //{get
        //    //get { return MachineName + ".southeastasia.cloudapp.azure.com"; }
        //}
        public string Username
        {
            get { return GenerateUserNameRandomName(); }
        }
        public string Password
        {
            get { return GeneratePasswordRandomName(); }
        }
        public string ResourceGroup {
            get { return "CS-PRD-" + CLPrefix.ToUpper(); }
        } 
        public string MachineName
        {
            get { return machineName.ToUpper(); }
            set { machineName = "PRD-" + CLPrefix.ToUpper() +"-" + GenerateTenRandomName(); }
        }
        public string ImageName { get; set; }
        public string ScheduledBy { get; set; }
        public int VETypeID { get; set; }
        public int UserID { get; set; }
        public int VEProfileID { get; set; }
        public int TenantID { get; set; }
        public string Size { get; set; }
    }
    public class ProvisionDetailsVM
    {
        public string CLPrefix;
        public string ResourceId { get; set; }
        public string FQDN { get; set; }       
        public string Username { get; set; }
        public string Password { get; set; }
        public string ResourceGroup { get; set; }
        public string MachineName { get; set; }
        public string ImageName { get; set; }
        public string ScheduledBy { get; set; }
        public int VETypeID { get; set; }
        public int UserID { get; set; }
        public int VEProfileID { get; set; }
        public int TenantID { get; set; }
        public string Size { get; set; }
    }

    public class VMDetails
    {
        public string VMName { get; set; }
        public string ResourceId { get; set; }
        public int RunBy { get; set; }

    }
    public class VMDeleteDetails
    {
        public int UserId { get; set; }
        public int VEProfileId { get; set; }
        public string SubscriptionId { get; set; }
        public string TenantId { get; set; }
        public string ApplicationId { get; set; }
        public string ApplicationSecret { get; set; }
        public string DeletedBy { get; set; }
        public string ClientCode { get; set; }
        public string VirtualMachines { get; set; }
        public string NewImageName { get; set; }

    }
}
