using System;
using System.Data.Entity;
using VMWAProvision.Model;

namespace VMWAProvision.Models
{
    public class CSDBContext : DbContext
    {

        public CSDBContext() : base(Environment.GetEnvironmentVariable("DBConnection", EnvironmentVariableTarget.Process))
        {
        }
        public DbSet<MachineLabs> MachineLabs { get; set; }
        public DbSet<MachineLogs> MachineLogs { get; set; }
        public DbSet<CloudLabsSchedules> CloudLabsSchedules { get; set; }
        public DbSet<VEProfiles> VEProfiles { get; set; }
        public DbSet<VETypes> VETypes { get; set; }
        public DbSet<CloudLabsGroups> CloudLabsGroups { get; set; }
        public DbSet<CloudLabUsers> CloudLabUsers { get; set; }
        public DbSet<VirtualEnvironments> VirtualEnvironments { get; set; }
        public DbSet<VEProfileLabCreditMappings> VEProfileLabCreditMappings { get; set; }
        public DbSet<CourseGrants> CourseGrants { get; set; }
        public DbSet<VMStatus> VMStatus { get; set; }
        public DbSet<VirtualEnvironmentImages> VirtualEnvironmentImages { get; set; }
        public DbSet<TimeSchedules> TimeSchedules { get; set; }
        //public DbSet<BusinessGroups> BusinessGroups { get; set; }
        //public DbSet<BusinessTypes> BusinessTypes { get; set; }

    }
}
