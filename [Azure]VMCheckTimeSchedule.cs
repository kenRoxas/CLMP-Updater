using System;
using Microsoft.Azure.Management.Compute.Fluent;
using Microsoft.Azure.Management.ContainerRegistry.Fluent.Models;
using System.Threading;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Host;
using Microsoft.Extensions.Logging;
using VMWAProvision.Models;
using static VMWAProvision.Helpers.Helper;
using static VMWAProvision.Helpers.AzureAz;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.IdentityModel.Tokens;
using System.Collections.Generic;
using System.Net.Http.Headers;
using System.Net.Http;
using Newtonsoft.Json;
using System.Text;
using System.Runtime.Intrinsics.X86;
using static Microsoft.ApplicationInsights.MetricDimensionNames.TelemetryContext;

namespace VMWAProvision
{
    public class VMCheckTimeSchedule
    {
        public static string RunBookBulkStart = GetEnvironmentVariable("RunBookBulkStart");

        [FunctionName("VMCheckTimeSchedule")]
        public static async Task Run([TimerTrigger("* * * * * *")] TimerInfo myTimer, ILogger log)
        {
            CSDBContext _db = new CSDBContext();
            CSDBTenantContext _dbTenant = new CSDBTenantContext();

            var vmList = new List<string>();

            HttpClient client = new HttpClient();
            client.BaseAddress = new Uri(RunBookBulkStart);
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            log.LogInformation("VMCheckTimeSchedule");

            try
            {      
                //NO CHECKING IF MACHINE IS EXPIRED
                var timeSchedule = _db.TimeSchedules.Where(q => q.IsEnabled).ToList();

                var tenantIds = _db.CloudLabUsers.Join(_db.MachineLabs, a => a.UserId, b => b.UserId, (a, b) => new { a, b })
                    .Join(_db.TimeSchedules, c => c.b.MachineLabsId, d => d.MachineLabsId, (c, d) => new { c, d })
                    .Join(_db.CloudLabsGroups, e => e.c.a.UserGroup, f => f.CloudLabsGroupID, (e, f) => new { e, f }).Select(q=>q.f.TenantId).ToList().Distinct();

                foreach (var tenantId in tenantIds) {

                    log.LogInformation($"tenantId = {tenantId}");
                    var group = _dbTenant.AzTenants.Where(q=>q.TenantId == tenantId).FirstOrDefault();

                    log.LogInformation($"group = {group}");

                    foreach (var item in timeSchedule)
                    {
                        try
                        {
                            log.LogInformation($"item = {item}");
                            var dateTimeUTC = DateTime.UtcNow;
                            var dateSched = item.StartTime;



                            var vm = _db.MachineLabs.Where(q => q.MachineLabsId == item.MachineLabsId).FirstOrDefault();
                            var schedTime = _db.CloudLabsSchedules.Where(q => q.MachineLabsId == item.MachineLabsId).FirstOrDefault();

                            TimeZoneInfo tst = TimeZoneInfo.FindSystemTimeZoneById(item.TimeZone);
                            //TimeZoneInfo tst = TimeZoneInfo.FindSystemTimeZoneById("Pacific Standard Time");
                            log.LogInformation($"tst = {tst}");

                            DateTime tstTime = TimeZoneInfo.ConvertTime(dateTimeUTC, TimeZoneInfo.Utc, tst);
                            DateTime timeDate = TimeZoneInfo.ConvertTime(dateSched, TimeZoneInfo.Utc, tst);

                            log.LogInformation($"tstTime = {tstTime}");


                            if (((timeDate - tstTime).TotalMinutes <= 2 && (timeDate - tstTime).TotalMinutes >= 0) && vm.MachineStatus != "Sched Starting" && schedTime.TimeRemaining >= 0)
                            {
                                var veType = _db.VEProfiles.Where(q => q.VEProfileID == vm.VEProfileId).Join(_db.VirtualEnvironments,
                                a => a.VirtualEnvironmentID,
                                b => b.VirtualEnvironmentID,
                                (a, b) => new { a, b }).Join(_db.VETypes, c => c.b.VETypeID, d => d.VETypeID, (c, d) => new { c, d }).Select(q => q.d).FirstOrDefault();

                                if (veType.VETypeID <= 2)
                                    vmList.Add(vm.VMName);

                                UpdateTimeSchedMachineLab(vm.ResourceId, 5, "Sched Starting");
                            }

                        }
                        catch (Exception ex)
                        {


                        }
                    }

                    if(vmList.Count > 0)
                    {

                        var data = new
                        {
                            applicationSubscriptionId = group.SubscriptionId,
                            applicationTenantId = group.ApplicationTenantId,
                            applicationSecret = group.ApplicationSecretKey,
                            applicationId = group.ApplicationId,
                            VirtualMachines = vmList
                        };

                        var dataMsg = JsonConvert.SerializeObject(data);



                        client.PostAsync("", new StringContent(dataMsg, Encoding.UTF8, "application/json"));
                    }

                }


            }
            catch (Exception ex)
            {

            }
        }
    }
}
