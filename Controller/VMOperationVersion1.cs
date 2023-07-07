//using Microsoft.Azure.Management.Compute.Fluent;
//using System;
//using System.Collections.Generic;
//using System.Text;
//using System.Threading.Tasks;
//using VMWAProvision.Models;
//using static VMWAProvision.Helpers.Helper;
//using static VMWAProvision.Helpers.AzureAz;
//using Microsoft.Extensions.Logging;
//using System.Linq;

//namespace VMWAProvision.Controller
//{
//    public class VMOperationVersion1
//    {
//        public async Task<string> Deallocate(MachineLabs ml, ILogger log)
//        {
//            var _azureProd = Az(log);
//            var currentDate = DateTime.UtcNow;
//            var status = true;

//            IVirtualMachine vmData = _azureProd.VirtualMachines.List().Where(vm => vm.Name.ToLower() == ml.VMName.ToLower()).FirstOrDefault();

//            log.LogInformation("Time Difference Before " + (currentDate - DateTime.UtcNow).Minutes);

//            if (vmData.PowerState.Value.Split("/")[1].ToLower() == "deallocated")
//            {
//                log.LogInformation("Status is Deallocated!");
//                UpdateMachineStatus(ml, log, 0, "");
//                status = !status;
//            }
//            else
//                await vmData.DeallocateAsync();

//            while (status)
//            {
//                IVirtualMachine vmData2 = _azureProd.VirtualMachines.List().Where(vm => vm.Name.ToLower() == ml.VMName.ToLower()).FirstOrDefault();

//                if (vmData2.PowerState.Value.Split("/")[1].ToLower() == "deallocated")
//                {
//                    log.LogInformation("Status is Deallocated!");
//                    UpdateMachineStatus(ml, log, 0, "");
//                    status = !status;
//                }
//            }

//            //  UpdateMachineStatus(ml, log, 0);


//            return "";
//        }
//        public async Task<string> DeallocateFailed(MachineLabs ml, ILogger log)
//        {
//            log.LogInformation("Enter DeallocateFailed");
//            var _azureProd = Az(log);
//            var currentDate = DateTime.UtcNow;

//            IVirtualMachine vmData = _azureProd.VirtualMachines.List().Where(vm => vm.Name.ToLower() == ml.VMName.ToLower()).FirstOrDefault();

//            log.LogInformation("Time Difference Before " + (currentDate - DateTime.UtcNow).Minutes);

//            await vmData.DeallocateAsync();

//            return "";
//        }

//    }

//}
