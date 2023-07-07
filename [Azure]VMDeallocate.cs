using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System.Net.Http;
using static VMWAProvision.Helpers.Helper;
using static VMWAProvision.Helpers.AzureAz;
using Microsoft.Azure.Management.ResourceManager.Fluent.Authentication;
using System.Linq;
using Microsoft.Azure.Management.Fluent;
using Microsoft.Azure.Management.ResourceManager.Fluent.Core;
using Microsoft.Azure.Management.ResourceManager.Fluent;
using Microsoft.Azure.Management.Compute.Fluent;
using System.Diagnostics;
using VMWAProvision.Models;
using System.Threading;

namespace VMWAProvision
{
    public static class VMDeallocate
    {
        [FunctionName("VMDeallocate")]
        public static async Task<IActionResult> Run([HttpTrigger(AuthorizationLevel.Function, "get", "post", Route = null)] HttpRequestMessage req, ILogger log)
        {
            try
            {
                CSDBContext _db = new CSDBContext();
                CSDBCustomerVMContext _dbCustomer = new CSDBCustomerVMContext();

                var currentDate = DateTime.UtcNow;
                var status = true;

                log.LogInformation("C# HTTP trigger function processed a request.");

                // var operationId = Activity.Current.RootId;
                var operationId = "";

                dynamic body = await req.Content.ReadAsStringAsync();
                var data = JsonConvert.DeserializeObject<VMDetails>(body as string);

                var _vm = _db.MachineLabs.Where(q => q.ResourceId == data.ResourceId).FirstOrDefault();

                var _azureProd = Az(log);

                IVirtualMachine vmData = _azureProd.VirtualMachines.List().Where(vm => vm.Name.ToLower() == data.VMName.ToLower()).FirstOrDefault();

                log.LogInformation("Time Difference Before " + (currentDate - DateTime.UtcNow).Minutes);

                if (vmData.PowerState.Value.Split("/")[1].ToLower() == "deallocated")
                {
                    log.LogInformation("Status is Deallocated!");
                    UpdateMachineLab(_vm.ResourceId, 0, 0, operationId, "Deallocated");
                    status = !status;
                }
                else
                    await vmData.DeallocateAsync();

                while (status)
                {
                    Thread.Sleep(30000);
                    IVirtualMachine vmData2 = _azureProd.VirtualMachines.List().Where(vm => vm.Name.ToLower() == data.VMName.ToLower()).FirstOrDefault();

                    if (vmData2.PowerState.Value.Split("/")[1].ToLower() == "deallocated")
                    {
                        log.LogInformation("Status is Deallocated!");
                        UpdateMachineLab(_vm.ResourceId, 0, 0, operationId, "Deallocated");
                        status = !status;
                    }
                }

                log.LogInformation("Time Difference After " + (currentDate - DateTime.UtcNow).Minutes);

                return new OkObjectResult("");

            }
            catch (Exception ex)
            {
                log.LogInformation($"Error {ex.Message}");
                return new OkObjectResult(ex.Message);

            }
        }
    }

}
