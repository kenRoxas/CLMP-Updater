using System;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Host;
using Microsoft.Extensions.Logging;
using static VMWAProvision.Helpers.Helper;
using static VMWAProvision.Helpers.AzureAz;
using System.Threading.Tasks;
using VMWAProvision.Controller;
using VMWAProvision.Models;
using System.Linq;
using Microsoft.AspNetCore.Mvc;
using System.Security.Cryptography;
using System.IO;
using System.Text;
using Microsoft.Azure.Management.Compute.Fluent;
using System.Threading;
using Microsoft.Azure.WebJobs.Extensions.Http;
using System.Net.Http;

namespace VMWAProvision
{
    public class VMUpdateAzureStarting
    {
        [FunctionName("VMUpdateAzureStarting")]
        public static async Task Run([TimerTrigger("* * * * * *")] TimerInfo myTimer, ILogger log)
        {
            try
            {
                log.LogInformation($"Start VMUpdateAzureStarting");

                var status = true;
                CSDBContext _db = new CSDBContext();
                CSDBTenantContext _dbTenant = new CSDBTenantContext();
                CSDBCustomerVMContext _dbCustomer = new CSDBCustomerVMContext();
                VMOperation deallocate = new VMOperation();

                var data = _db.MachineLabs.Where(q => q.IsStarted == 5).Join(_db.CloudLabUsers, a => a.UserId, b => b.UserId, (a, b) => new { a, b })
                    .Join(_db.VEProfiles, c => c.a.VEProfileId, d => d.VEProfileID, (c, d) => new { c, d }).Join(_db.VirtualEnvironments, e => e.d.VirtualEnvironmentID, f => f.VirtualEnvironmentID, (e, f) => new { e, f })
                    .Join(_db.MachineLogs, g => g.e.c.a.ResourceId, h => h.ResourceId, (g, h) => new { g, h }).Where(j => j.g.f.VETypeID <= 4)
                    .Select(w => new { ml = w.g.e.c.a, TenantId = w.g.e.c.b.TenantId, w.g.f.VETypeID, w.h.ModifiedDate, w.h.LastStatus }).ToList();

                var _azure = Az(log);

                foreach (var item in data)
                {
                    if (DateTime.UtcNow.Subtract(item.ModifiedDate.Value).TotalMinutes >= 3)
                    {
                        var ml = _db.MachineLabs.Where(q => q.ResourceId == item.ml.ResourceId).FirstOrDefault();

                        if (ml.MachineStatus == "Sched Starting")
                        {
                            UpdateTimeSchedMachineLab(ml.ResourceId, 1, "Sched Running");
                        }
                        else
                        {
                            await Task.Run(() =>
                            {
                                TenantDetails tenants = new TenantDetails();

                                var dataTenant = _dbTenant.AzTenants.Where(q => q.TenantId == item.TenantId).FirstOrDefault();

                                tenants.EnvironmentCode = dataTenant.EnvironmentCode;
                                tenants.GuacamoleURL = dataTenant.GuacamoleURL;
                                tenants.GuacConnection = dataTenant.GuacConnection;
                                tenants.SubscriptionKey = dataTenant.SubscriptionId;
                                tenants.TenantId = dataTenant.TenantId;
                                tenants.TenantKey = dataTenant.ApplicationTenantId;

                                try
                                {
                                    var customer = _dbCustomer.VirtualMachineDetails.Where(q => q.ResourceId == item.ml.ResourceId).FirstOrDefault();

                                    log.LogInformation($"ResourceId = {item.ml.ResourceId}");

                                    var vmData = _azure.VirtualMachines.List().Where(vm => vm.Name.ToLower() == ml.VMName.ToLower()).FirstOrDefault();

                                    log.LogInformation($"Status1 = {vmData.PowerState.Value.Split("/")[1].ToLower()}");

                                    if (vmData.PowerState.Value.Split("/")[1].ToLower() == "running")
                                    {
                                        if (ml.RunningBy == 2)
                                            UpdateMachineLab(ml.ResourceId, 1, 2, "", "Running");
                                        else
                                            UpdateMachineLab(ml.ResourceId, 1, 1, "", "Running");

                                        log.LogInformation("Status is running");
                                        status = !status;
                                        Thread.Sleep(30000);
                                    }
                                    else
                                    {
                                        log.LogInformation("Starting");
                                        vmData.StartAsync();
                                    }

                                    while (status)
                                    {
                                        Thread.Sleep(30000);
                                        log.LogInformation("Updating");

                                        var vmData2 = _azure.VirtualMachines.List().Where(vm => vm.Name.ToLower() == ml.VMName.ToLower()).FirstOrDefault();

                                        if (vmData2.PowerState.Value.Split("/")[1].ToLower() == "running")
                                        {
                                            log.LogInformation("Status is running!");
                                            if (ml.RunningBy == 2)
                                                UpdateMachineLab(ml.ResourceId, 1, 2, "", "Running");
                                            else
                                                UpdateMachineLab(ml.ResourceId, 1, 1, "", "Running");
                                            status = !status;
                                        }
                                    }


                                }
                                catch (Exception e)
                                {
                                    log.LogInformation($"Error {e.Message}");

                                }




                            });


                        }


                    }
                    else if (DateTime.UtcNow.Subtract(item.ModifiedDate.Value).TotalMinutes > 61)
                    {
                        var ml = _db.MachineLabs.Where(q => q.ResourceId == item.ml.ResourceId).FirstOrDefault();

                        UpdateMachineStatus(ml, log, 3, "Too long to provision");
                    }
                }
                //return new OkObjectResult("tapos na");
            }
            catch (Exception ex)
            {
                log.LogInformation($"VMUpdateAzure = {ex.Message}");

            }
        }
    }
}
