using System;
using System.Data.Entity;

namespace VMWAProvision.Models
{
    public class CSDBCustomerVMContext : DbContext
    {
        public CSDBCustomerVMContext() : base(Environment.GetEnvironmentVariable("DBCustomerVMConnection", EnvironmentVariableTarget.Process))
        {
        }
        public DbSet<VirtualMachineDetails> VirtualMachineDetails { get; set; }
        public DbSet<VMStatus> VMStatus { get; set; }

    }
}
