using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Text;
using VMWAProvision.Models;

namespace VMWAProvision.Models
{
    public class VirtualMachineDetails
    {
        [Key]
        public int VirtualMachineId { get; set; }
        public string ResourceId { get; set; }
        public int Status { get; set; }
        public string VMName { get; set; }
        public string FQDN { get; set; }
        public string OperationId { get; set; }
        public DateTime DateLastModified { get; set; }
        public DateTime DateCreated { get; set; }

        //private CSDBCustomerVMContext _dbCustomer;

        //public VirtualMachineDetails()
        //{
        //    // Create a new instance of the context.
        //    _dbCustomer = new CSDBCustomerVMContext();
        //}
        //public void Dispose()
        //{
        //    _dbCustomer.Dispose();
        //    _dbCustomer = null;
        //}
    }
}
