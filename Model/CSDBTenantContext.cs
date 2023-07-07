using System;
using System.Data.Entity;

namespace VMWAProvision.Models
{
    public class CSDBTenantContext : DbContext
    {
        public CSDBTenantContext() : base(Environment.GetEnvironmentVariable("DBTenantConnection", EnvironmentVariableTarget.Process))
        {
        }
        public DbSet<AzTenants> AzTenants { get; set; }

    }
}
