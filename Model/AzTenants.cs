using System;
using System.ComponentModel.DataAnnotations;

namespace VMWAProvision.Models
{
    //public class AzTenants
    //{
    //    [Key]
    //    public int TenantId { get; set; }
    //    public string TenantKey { get; set; }
    //    //public string Location { get; set; }
    //    public string ClientCode { get; set; }
    //    public string EnvironmentCode { get; set; }
    //    public string SubscriptionKey { get; set; }
    //    public string GuacConnection { get; set; }
    //    public string GuacamoleURL { get; set; }
    //    public string CreatedBy { get; set; }
    //    public string ClientKey { get; set; }
    //    public string ClientSecret { get; set; }
    //    public DateTime DateCreated { get; set; }
    //}
    public class AzTenants
    {
        [Key]
        public int TenantId { get; set; }
        // public string TenantKey { get; set; }
        //public string Location { get; set; }
        public string ClientCode { get; set; }
        public string EnvironmentCode { get; set; }
        //public string SubscriptionKey { get; set; }
        public string GuacConnection { get; set; }
        public string Regions { get; set; }
        public string GuacamoleURL { get; set; }
        public string CreatedBy { get; set; }
        public string ClientKey { get; set; }
        public string ClientSecret { get; set; }
        public DateTime DateCreated { get; set; }
        public string SubscriptionId { get; set; }
        public string ApplicationId { get; set; }
        public string ApplicationTenantId { get; set; }
        public string ApplicationSecretKey { get; set; }
        public bool IsFireWall { get;set; }
        //public int BusinessId { get; set; }
    }


    public class TenantDetails
    {
        public int TenantId { get; set; }
        public string TenantKey { get; set; }
        public string SubscriptionKey { get; set; }
        public string GuacConnection { get; set; }
        public string GuacamoleURL { get; set; }
        public string EnvironmentCode { get; set; }
    }
}
