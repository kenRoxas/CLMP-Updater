using System;
using System.Data.Entity;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Azure.Management.Compute.Fluent;
using Microsoft.Azure.Management.Fluent;
using Microsoft.Azure.Management.ResourceManager.Fluent;
using Microsoft.Azure.Management.ResourceManager.Fluent.Authentication;
using Microsoft.Azure.Management.ResourceManager.Fluent.Core;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Azure.WebJobs.Host;
using Microsoft.Extensions.Logging;
using MySql.Data.MySqlClient;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using VMWAProvision.Models;
using static VMWAProvision.Helpers.Helper;
namespace VMWAProvision
{
    public class VMAWSStarting
    {
        public static string AWSVM = GetEnvironmentVariable("AWSVM");

        [FunctionName("VMAWSStarting")]
        public static async Task Run([TimerTrigger("* */2 * * * *")] TimerInfo myTimer, ILogger log)
        //public static async Task<string> Run([HttpTrigger(AuthorizationLevel.Function, "get", "post", Route = null)] HttpRequestMessage req, ILogger log)

        {
            CSDBContext _db = new CSDBContext();
            CSDBTenantContext _dbTenant = new CSDBTenantContext();
            log.LogInformation($"C# Timer trigger function executed at: {DateTime.Now}");
            DateTime currentDate = DateTime.UtcNow;

            try
            {
                DateTime currentDate1 = DateTime.UtcNow;

                var machineUsers = _db.MachineLabs.GroupJoin(_db.CloudLabsSchedules, ml => ml.MachineLabsId, cs => cs.MachineLabsId, (ml, cs) => new { ml, cs })
                    .Join(_db.VEProfiles, csml => csml.ml.VEProfileId, v => v.VEProfileID, (csml, v) => new { csml, v })
                    .Join(_db.VirtualEnvironments, ve => ve.v.VirtualEnvironmentID, vi => vi.VirtualEnvironmentID, (ve, vi) => new { ve, vi })
                    .Join(_db.CloudLabUsers, csml => csml.ve.csml.ml.UserId, cu => cu.UserId, (csml, cu) => new { csml, cu })
                    .Join(_db.CloudLabsGroups, csml => csml.cu.UserGroup, cg => cg.CloudLabsGroupID, (csml, cg) => new { csml, cg })
                    .Join(_db.MachineLogs, csml => csml.csml.csml.ve.csml.ml.ResourceId, mlg => mlg.ResourceId, (csml, mlg) => new { csml, mlg })
                    .Where(x => x.csml.csml.csml.vi.VETypeID == 9).Select(q => new MachineVMStatus
                    {
                        ResourceId = q.csml.csml.csml.ve.csml.ml.ResourceId,
                        VEProfileId = q.csml.csml.csml.ve.v.VEProfileID,
                        UserId = q.csml.csml.cu.UserId,
                        IsStarted = q.csml.csml.csml.ve.csml.ml.IsStarted,
                        TenantId = q.csml.csml.cu.TenantId,
                        VETypeID = q.csml.csml.csml.vi.VETypeID,
                        VMName = q.csml.csml.csml.ve.csml.ml.VMName,
                        DateProvision = q.csml.csml.csml.ve.csml.ml.DateProvision,
                        RunningBy = q.csml.csml.csml.ve.csml.ml.RunningBy,
                        TimeRemaining = q.csml.csml.csml.ve.csml.cs.Any(w => w.MachineLabsId == q.csml.csml.csml.ve.csml.ml.MachineLabsId) ? q.csml.csml.csml.ve.csml.cs.Where(w => w.MachineLabsId == q.csml.csml.csml.ve.csml.ml.MachineLabsId).FirstOrDefault().TimeRemaining : null,
                        InstructorLabHours = q.csml.csml.csml.ve.csml.cs.Any(w => w.MachineLabsId == q.csml.csml.csml.ve.csml.ml.MachineLabsId) ? q.csml.csml.csml.ve.csml.cs.Where(w => w.MachineLabsId == q.csml.csml.csml.ve.csml.ml.MachineLabsId).FirstOrDefault().InstructorLabHours : null,
                        MachineStatus = q.csml.csml.csml.ve.csml.ml.MachineStatus,
                        GroupID = q.csml.csml.cu.UserGroup,
                        MachineLabsId = q.csml.csml.csml.ve.csml.ml.MachineLabsId,
                        ModifiedDate = DbFunctions.DiffMinutes(q.mlg.ModifiedDate, currentDate).Value,
                        GuacDNS = q.csml.csml.csml.ve.csml.ml.GuacDNS
                    }).Where(q => q.IsStarted == 5).ToList();

                log.LogInformation($"Total machines : {machineUsers.Count}");

                foreach (var item in machineUsers)
                {
                    await Task.Run(() =>
                    {
                        log.LogInformation($"Enter loop VM AWS Update ResourceId: {item.ResourceId}");
                        var ModifiedDate = _db.MachineLogs.Where(q => q.ResourceId == item.ResourceId).FirstOrDefault().ModifiedDate;

                        var tenants = _dbTenant.AzTenants.Where(q => q.TenantId == item.TenantId).Select(w => new TenantDetails
                        {
                            EnvironmentCode = w.EnvironmentCode,
                            GuacamoleURL = w.GuacamoleURL,
                            GuacConnection = w.GuacConnection,
                            SubscriptionKey = w.SubscriptionId,
                            TenantId = w.TenantId,
                            TenantKey = w.ApplicationTenantId
                        }).FirstOrDefault();

                        try
                        {
                            if (item.ModifiedDate >= 2)
                            {
                                Thread.Sleep(15000);
                                
                                UpdateDBStatus(item, tenants, log).ConfigureAwait(true);

                            }
                        }
                        catch (Exception e)
                        {
                            log.LogInformation($"Catch : {e.Message}");
                        }
                    });

                    log.LogInformation($"C# Timer trigger function finished at: {DateTime.Now}");
                }

            }
            catch (Exception e)
            {
                log.LogInformation($"Error e: {e.Message}");

            }
            //return "";
        }
        private static async Task UpdateDBStatus(MachineVMStatus _vmStatus, TenantDetails tenant, ILogger log)
        {
            try
            {
                CSDBContext _db = new CSDBContext();
                CSDBTenantContext _dbTenant = new CSDBTenantContext();

                var ml = _db.MachineLabs.Where(q => q.ResourceId == _vmStatus.ResourceId).FirstOrDefault();
                var mls = _db.MachineLogs.Where(q => q.ResourceId == _vmStatus.ResourceId).FirstOrDefault();

                HttpClient clientAWS = new HttpClient();
                HttpResponseMessage responseGetDetails = null;
                JObject getDetails = null;
                clientAWS.BaseAddress = new Uri(AWSVM);
                clientAWS.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

                ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;

                var jsonDetailsHB = new
                {
                    instance_id = _vmStatus.ResourceId,
                    region = "ap-southeast-1"
                };

                var jsonDataHB = JsonConvert.SerializeObject(jsonDetailsHB);

                responseGetDetails = await clientAWS.PostAsync("dev/get_vm_details", new StringContent(jsonDataHB, Encoding.UTF8, "application/json"));
                getDetails = JObject.Parse(responseGetDetails.Content.ReadAsStringAsync().Result);

                var isRunning = getDetails.SelectToken("Reservations[0].Instances[0].State.Name").ToString();
                var isTrue = true;
                var iterator = 0;

                while (isTrue)
                {
                    iterator++;

                    if (iterator == 5)
                    {
                        if (isRunning.ToLower() == "running" && ml.IsStarted != 1)
                        {
                            var DNS = getDetails.SelectToken("Reservations[0].Instances[0].PublicDnsName").ToString();

                            if (ml.GuacDNS == null)
                            {
                                var guacURL = AddMachineToDatabase(ml.VMName, tenant, "cloudswyft", "c5w1N4W5c2", _vmStatus.VETypeID, DNS, log);

                                ml.GuacDNS = guacURL;
                                _db.SaveChanges();
                            }

                            if (ml.FQDN != DNS)
                            {
                                var guacUrl = AddMachineToDatabase(ml.VMName, tenant, "cloudswyft", "c5w1N4W5c2", _vmStatus.VETypeID, DNS, log);

                                ml.IsStarted = 1;
                                ml.MachineStatus = "Running";
                                ml.FQDN = DNS;
                                if (guacUrl != "")
                                    ml.GuacDNS = guacUrl;
                                ml.RunningBy = 1;

                                mls.Logs = "(VM-Running)" + DateTime.UtcNow + "---" + mls.Logs;
                                mls.LastStatus = "VM-Running";
                                mls.ModifiedDate = DateTime.UtcNow;
                                _db.Entry(mls).State = EntityState.Modified;

                                _db.Entry(ml).State = EntityState.Modified;

                                _db.SaveChanges();
                            }
                        }
                        isTrue = false;

                    }
                }

                if (isRunning == "stopped" && ml.IsStarted != 0)
                {
                    HttpClient clientremove = new HttpClient();

                    clientremove.BaseAddress = new Uri(AWSVM);
                    clientremove.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

                    var data = new
                    {
                        instance_id = ml.ResourceId,
                        ip_address = ml.IpAddress,
                        region = "ap-southeast-1",
                        action_type = "REMOVE"
                    };

                    var dataRemove = JsonConvert.SerializeObject(data);
                    await clientremove.PostAsync("dev/update_security_group", new StringContent(dataRemove, Encoding.UTF8, "application/json"));

                    ml.IsStarted = 0;
                    ml.MachineStatus = "Deallocated";
                    ml.RunningBy = 0;
                    ml.IpAddress = null;
                    _db.Entry(ml).State = EntityState.Modified;
                    _db.SaveChanges();

                    mls.Logs = "(VM-Deallocated)" + DateTime.UtcNow + "---" + mls.Logs;
                    mls.LastStatus = "VM-Deallocated";
                    mls.ModifiedDate = DateTime.UtcNow;
                    _db.Entry(mls).State = EntityState.Modified;

                    _db.SaveChanges();
                }
            }
            catch (Exception e)
            {
                log.LogInformation($"Error Updating AWS: {e.Message}");
            }
        }
    }
}
