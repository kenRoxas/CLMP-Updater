using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Host;
using Microsoft.Extensions.Logging;
using VMWAProvision.Controller;
using VMWAProvision.Models;
using static VMWAProvision.Helpers.Helper;
using static VMWAProvision.Helpers.AzureAz;
using System.Threading;

namespace VMWAProvision
{
    public class _Azure_VMHeartBeatTimeSchedule
    {
        [FunctionName("Azure_VMHeartBeatTimeSchedule")]
        public static async Task Run([TimerTrigger("* * * * * *")] TimerInfo myTimer, ILogger log)
        {
            try
            {          
                log.LogInformation($"Start _Azure_VMHeartBeatTimeSchedule");
                var status = true;

                CSDBContext _db = new CSDBContext();
                CSDBTenantContext _dbTenant = new CSDBTenantContext();
                CSDBCustomerVMContext _dbCustomer = new CSDBCustomerVMContext();
                VMOperation deallocate = new VMOperation();

                var _azure = Az(log);

                var data = _db.MachineLabs.Where(q => q.IsStarted == 1).Join(_db.CloudLabUsers, a => a.UserId, b => b.UserId, (a, b) => new { a, b })
                    .Join(_db.VEProfiles, c => c.a.VEProfileId, d => d.VEProfileID, (c, d) => new { c, d }).Join(_db.VirtualEnvironments, e => e.d.VirtualEnvironmentID, f => f.VirtualEnvironmentID, (e, f) => new { e, f })
                    .Join(_db.MachineLogs, g => g.e.c.a.ResourceId, h => h.ResourceId, (g, h) => new { g, h }).Where(j => (j.g.f.VETypeID == 1 || j.g.f.VETypeID == 3))
                    .Select(w => new { ml = w.g.e.c.a, TenantId = w.g.e.c.b.TenantId, w.g.f.VETypeID, w.h.ModifiedDate, w.h.LastStatus }).ToList();

                var timeSchedule = _db.TimeSchedules.Where(q => q.IsEnabled).ToList();

                foreach (var item in data)
                {                    
                    try
                    {
                        
                        var idleTime = _db.TimeSchedules.Where(q=>q.MachineLabsId == item.ml.MachineLabsId).FirstOrDefault().IdleTime;

                        if (DateTime.UtcNow.Subtract(item.ModifiedDate.Value).TotalMinutes >= idleTime && item.LastStatus == "Sched Running")
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
                                    var customer = _dbCustomer.VirtualMachineDetails.Where(q => q.ResourceId == item.ml.ResourceId).FirstOrDefault();

                                    log.LogInformation($"ResourceId = {item.ml.ResourceId}");

                                    var vmData = _azure.VirtualMachines.List().Where(vm => vm.Name.ToLower() == ml.VMName.ToLower()).FirstOrDefault();

                                    log.LogInformation($"Status1 = {vmData.PowerState.Value.Split("/")[1].ToLower()}");

                                    if (vmData.PowerState.Value.Split("/")[1].ToLower() == "deallocated")
                                    {
                                        log.LogInformation("Status is deallocated");

                                        if (ml.MachineStatus == "Sched Starting")
                                            UpdateTimeSchedMachineLab(ml.ResourceId, 0, "Sched Stopped");
                                        else
                                            UpdateMachineLab(ml.ResourceId, 0, 0, "", "Deallocated");

                                        //UpdateMachineLab(ml.ResourceId, 0, 0, "");

                                        status = !status;
                                        Thread.Sleep(5000);
                                    }
                                    else
                                    {
                                        log.LogInformation("Shut Down");
                                        UpdateTimeSchedMachineLab(ml.ResourceId, 2, "Sched Stopping");

                                        var cs = _db.CloudLabsSchedules.Where(q => q.MachineLabsId == ml.MachineLabsId).FirstOrDefault();
                                        cs.TimeRemaining = cs.TimeRemaining - (idleTime * 60);
                                        _db.SaveChanges();

                                        vmData.DeallocateAsync();
                                    }

                                    //while (status)
                                    //{
                                    //    Thread.Sleep(5000);
                                    //    log.LogInformation("Updating");

                                    //    var vmData2 = _azure.VirtualMachines.List().Where(vm => vm.Name.ToLower() == ml.VMName.ToLower()).FirstOrDefault();

                                    //    if (vmData2.PowerState.Value.Split("/")[1].ToLower() == "deallocated")
                                    //    {
                                    //        log.LogInformation("Status is deallocated!");
                                    //        UpdateMachineLab(ml.ResourceId, 0, 0, "");

                                    //        //deduct consumed hours
                                    //        var cs = _db.CloudLabsSchedules.Where(q => q.MachineLabsId == ml.MachineLabsId).FirstOrDefault();
                                    //        //cs.TimeRemaining = cs.TimeRemaining - DateTime.UtcNow.Subtract(item.ModifiedDate.Value).TotalSeconds;
                                    //        cs.TimeRemaining = cs.TimeRemaining - (idleTime * 60);
                                    //        _db.SaveChanges();

                                    //        status = !status;
                                    //    }
                                    //}

                                }
                                catch (Exception e)
                                {
                                    log.LogInformation($"Error {e.Message}");

                                }
                            });


                        }

                    }
                    catch (Exception ex) { }
                }
                    
                    
            }
            catch (Exception ex)
            {

            }
        }
    }
}
