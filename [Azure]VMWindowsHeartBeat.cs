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
using System.Threading;

namespace VMWAProvision
{
    public class VMAzureWindowsHeartBeat
    {
        [FunctionName("VMAzureWindowsHeartBeat")]
        public static async Task Run([TimerTrigger("%CRON_TIME_HeartBeat%")] TimerInfo myTimer, ILogger log)
        {
            try
            {
                log.LogInformation($"Start VMAzureWindowsHeartBeat");
                var status = true;

                CSDBContext _db = new CSDBContext();
                CSDBTenantContext _dbTenant = new CSDBTenantContext();
                CSDBCustomerVMContext _dbCustomer = new CSDBCustomerVMContext();
                VMOperation deallocate = new VMOperation();

                var data = _db.MachineLabs.Where(q => q.IsStarted == 1).Join(_db.CloudLabUsers, a => a.UserId, b => b.UserId, (a, b) => new { a, b })
                    .Join(_db.VEProfiles, c => c.a.VEProfileId, d => d.VEProfileID, (c, d) => new { c, d }).Join(_db.VirtualEnvironments, e => e.d.VirtualEnvironmentID, f => f.VirtualEnvironmentID, (e, f) => new { e, f })
                    .Join(_db.MachineLogs, g => g.e.c.a.ResourceId, h => h.ResourceId, (g, h) => new { g, h }).Where(j => (j.g.f.VETypeID == 1 || j.g.f.VETypeID == 3)).Select(w => new { ml = w.g.e.c.a, TenantId = w.g.e.c.b.TenantId, w.g.f.VETypeID, w.h.ModifiedDate, w.h.LastStatus }).ToList();


                foreach (var item in data)
                {
                    if (DateTime.UtcNow.Subtract(item.ModifiedDate.Value).TotalMinutes >= 15 && item.ml.MachineStatus != "Sched Running" && item.LastStatus != "Run HeartBeat")
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

                            var ml = _db.MachineLabs.Where(q => q.ResourceId == item.ml.ResourceId).FirstOrDefault();

                            try
                            {
                                var _azure = Az(log);
                                var customer = _dbCustomer.VirtualMachineDetails.Where(q => q.ResourceId == item.ml.ResourceId).FirstOrDefault();

                                log.LogInformation($"ResourceId = {item.ml.ResourceId}");

                                var vmData = _azure.VirtualMachines.List().Where(vm => vm.Name.ToLower() == ml.VMName.ToLower()).FirstOrDefault();

                                log.LogInformation($"Status1 = {vmData.PowerState.Value.Split("/")[1].ToLower()}");

                                if (vmData.PowerState.Value.Split("/")[1].ToLower() == "deallocated")
                                {
                                    log.LogInformation("Status is deallocated");
                                    UpdateMachineLab(ml.ResourceId, 0, 0, "", "Unlaunched-Deallocated");
                                    status = !status;
                                    Thread.Sleep(30000);
                                }
                                else
                                {
                                    log.LogInformation("Shut Down");
                                    vmData.DeallocateAsync();
                                }

                                while (status)
                                {
                                    Thread.Sleep(30000);
                                    log.LogInformation("Updating");

                                    var vmData2 = _azure.VirtualMachines.List().Where(vm => vm.Name.ToLower() == ml.VMName.ToLower()).FirstOrDefault();

                                    if (vmData2.PowerState.Value.Split("/")[1].ToLower() == "deallocated")
                                    {
                                        log.LogInformation("Status is deallocated!");
                                        UpdateMachineLab(ml.ResourceId, 0, 0, "", "Unlaunched-Deallocated");

                                        //deduct consumed hours
                                        var cs = _db.CloudLabsSchedules.Where(q => q.MachineLabsId == ml.MachineLabsId).FirstOrDefault();
                                        cs.TimeRemaining = cs.TimeRemaining - DateTime.UtcNow.Subtract(item.ModifiedDate.Value).TotalSeconds;
                                        _db.SaveChanges();

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
