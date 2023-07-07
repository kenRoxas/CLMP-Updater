//using System;
//using System.Data.Entity;
//using System.IO;
//using System.Linq;
//using System.Net;
//using System.Net.Http;
//using System.Net.Http.Headers;
//using System.Security.Cryptography;
//using System.Text;
//using System.Threading;
//using System.Threading.Tasks;
//using Microsoft.AspNetCore.Http;
//using Microsoft.Azure.Management.Compute.Fluent;
//using Microsoft.Azure.Management.Fluent;
//using Microsoft.Azure.Management.ResourceManager.Fluent;
//using Microsoft.Azure.Management.ResourceManager.Fluent.Authentication;
//using Microsoft.Azure.Management.ResourceManager.Fluent.Core;
//using Microsoft.Azure.WebJobs;
//using Microsoft.Azure.WebJobs.Extensions.Http;
//using Microsoft.Azure.WebJobs.Host;
//using Microsoft.Extensions.Logging;
//using MySql.Data.MySqlClient;
//using Newtonsoft.Json;
//using Newtonsoft.Json.Linq;
//using VMWAProvision.Models;
//using static VMWAProvision.Helpers.Helper;
//namespace VMWAProvision
//{
//    public class VMAWSHeartBeat
//    {
//        public static string AWSVM = GetEnvironmentVariable("AWSVM");
//        public static string AWSStartStop = GetEnvironmentVariable("AWSStartStop");

//        [FunctionName("VMAWSHeartBeat")]
//        public static async Task Run([TimerTrigger("0 */5 * * * *")] TimerInfo myTimer, ILogger log)
//        //public static async Task Run([HttpTrigger(AuthorizationLevel.Function, "get", "post", Route = null)] HttpRequestMessage req, ILogger log)
//        {
//            CSDBContext _db = new CSDBContext();
//            CSDBTenantContext _dbTenant = new CSDBTenantContext();
//            log.LogInformation($"C# Timer trigger function executed at: {DateTime.Now}");
//            DateTime currentDate = DateTime.UtcNow;

//            try
//            {
//                DateTime currentDate1 = DateTime.UtcNow;

//                var machineUsers = _db.MachineLabs.GroupJoin(_db.CloudLabsSchedules, ml => ml.MachineLabsId, cs => cs.MachineLabsId, (ml, cs) => new { ml, cs })
//                    .Join(_db.VEProfiles, csml => csml.ml.VEProfileId, v => v.VEProfileID, (csml, v) => new { csml, v })
//                    .Join(_db.VirtualEnvironments, ve => ve.v.VirtualEnvironmentID, vi => vi.VirtualEnvironmentID, (ve, vi) => new { ve, vi })
//                    .Join(_db.CloudLabUsers, csml => csml.ve.csml.ml.UserId, cu => cu.UserId, (csml, cu) => new { csml, cu })
//                    .Join(_db.CloudLabsGroups, csml => csml.cu.UserGroup, cg => cg.CloudLabsGroupID, (csml, cg) => new { csml, cg })
//                    .Join(_db.MachineLogs, csml => csml.csml.csml.ve.csml.ml.ResourceId, mlg => mlg.ResourceId, (csml, mlg) => new { csml, mlg })
//                    .Where(x => x.csml.csml.csml.vi.VETypeID == 9).Select(q => new
//                    {
//                        ResourceId = q.csml.csml.csml.ve.csml.ml.ResourceId,
//                        VEProfileId = q.csml.csml.csml.ve.v.VEProfileID,
//                        UserId = q.csml.csml.cu.UserId,
//                        IsStarted = q.csml.csml.csml.ve.csml.ml.IsStarted,
//                        TenantId = q.csml.csml.cu.TenantId,
//                        VETypeID = q.csml.csml.csml.vi.VETypeID,
//                        VMName = q.csml.csml.csml.ve.csml.ml.VMName,
//                        DateProvision = q.csml.csml.csml.ve.csml.ml.DateProvision,
//                        RunningBy = q.csml.csml.csml.ve.csml.ml.RunningBy,
//                        TimeRemaining = q.csml.csml.csml.ve.csml.cs.Any(w => w.MachineLabsId == q.csml.csml.csml.ve.csml.ml.MachineLabsId) ? q.csml.csml.csml.ve.csml.cs.Where(w => w.MachineLabsId == q.csml.csml.csml.ve.csml.ml.MachineLabsId).FirstOrDefault().TimeRemaining : null,
//                        InstructorLabHours = q.csml.csml.csml.ve.csml.cs.Any(w => w.MachineLabsId == q.csml.csml.csml.ve.csml.ml.MachineLabsId) ? q.csml.csml.csml.ve.csml.cs.Where(w => w.MachineLabsId == q.csml.csml.csml.ve.csml.ml.MachineLabsId).FirstOrDefault().InstructorLabHours : null,
//                        MachineStatus = q.csml.csml.csml.ve.csml.ml.MachineStatus,
//                        GroupID = q.csml.csml.cu.UserGroup,
//                        MachineLabsId = q.csml.csml.csml.ve.csml.ml.MachineLabsId,
//                        ModifiedDate = q.mlg.ModifiedDate,
//                        GuacDNS = q.csml.csml.csml.ve.csml.ml.GuacDNS
//                    }).Where(q => q.IsStarted == 1).ToList();

//                log.LogInformation($"Total machines : {machineUsers.Count}");

//                foreach (var item in machineUsers)
//                {
//                    try
//                    {
//                        if (DateTime.UtcNow.Subtract(item.ModifiedDate.Value).TotalMinutes >= 15)
//                        {
//                            await Task.Run(() =>
//                            {
//                                log.LogInformation($"Enter loop VM AWS Update ResourceId: {item.ResourceId}");
//                                var ModifiedDate = _db.MachineLogs.Where(q => q.ResourceId == item.ResourceId).FirstOrDefault().ModifiedDate;

//                                var tenants = _dbTenant.AzTenants.Where(q => q.TenantId == item.TenantId).Select(w => new TenantDetails
//                                {
//                                    EnvironmentCode = w.EnvironmentCode,
//                                    GuacamoleURL = w.GuacamoleURL,
//                                    GuacConnection = w.GuacConnection,
//                                    SubscriptionKey = w.SubscriptionId,
//                                    TenantId = w.TenantId,
//                                    TenantKey = w.ApplicationTenantId
//                                }).FirstOrDefault();

//                                try
//                                {
//                                    Thread.Sleep(15000);
//                                    UpdateDBStatus(item.ResourceId, tenants, log).ConfigureAwait(true);
//                                }
//                                catch (Exception e)
//                                {
//                                    log.LogInformation($"Catch : {e.Message}");
//                                }
//                            });

//                        }

//                    }
//                    catch (Exception e)
//                    {

//                    }

//                    log.LogInformation($"C# Timer trigger function finished at: {DateTime.Now}");
//                }

//            }
//            catch (Exception e)
//            {
//                log.LogInformation($"Error e: {e.Message}");

//            }
//        }

//        private static async Task UpdateDBStatus(string resourceId, TenantDetails tenant, ILogger log)
//        {
//            try
//            {
//                DateTime dateUtc = DateTime.UtcNow;

//                CSDBContext _db = new CSDBContext();
//                CSDBTenantContext _dbTenant = new CSDBTenantContext();

//                var ml = _db.MachineLabs.Where(q => q.ResourceId == resourceId).FirstOrDefault();
//                var mls = _db.MachineLogs.Where(q => q.ResourceId == resourceId).FirstOrDefault();

//                HttpClient clientAWS = new HttpClient();
//                HttpResponseMessage responseGetDetails = null;
//                JObject getDetails = null;
//                clientAWS.BaseAddress = new Uri(AWSVM);
//                clientAWS.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

//                HttpClient clientAWSStartStop = new HttpClient();
//                clientAWSStartStop.BaseAddress = new Uri(AWSStartStop);
//                clientAWSStartStop.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

//                ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;

//                var jsonDetailsHB = new
//                {
//                    instance_id = resourceId,
//                    region = "ap-southeast-1"
//                };

//                var jsonDataHB = JsonConvert.SerializeObject(jsonDetailsHB);

//                var dataJsonStop = new
//                {
//                    ec2_id = resourceId,
//                    region = "ap-southeast-1"
//                };
//                var dataParseStop = JsonConvert.SerializeObject(dataJsonStop);

//                var ss = await clientAWSStartStop.PostAsync("dev/mac_instance/stop_mac_instance", new StringContent(dataParseStop, Encoding.UTF8, "application/json"));

//                responseGetDetails = await clientAWS.PostAsync("dev/get_vm_details", new StringContent(jsonDataHB, Encoding.UTF8, "application/json"));

//                getDetails = JObject.Parse(responseGetDetails.Content.ReadAsStringAsync().Result);

//                var isRunning = getDetails.SelectToken("Reservations[0].Instances[0].State.Name").ToString();

//                if (isRunning.ToLower() == "stopped" && ml.IsStarted != 0)
//                {

//                    var v = _db.MachineLabs.Where(q => q.ResourceId == ml.ResourceId).FirstOrDefault();

//                    HttpClient clientremove = new HttpClient();

//                    clientremove.BaseAddress = new Uri(AWSVM);
//                    clientremove.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

//                    var data = new
//                    {
//                        instance_id = v.ResourceId,
//                        ip_address = v.IpAddress,
//                        region = "ap-southeast-1",
//                        action_type = "REMOVE"
//                    };

//                    var dataRemove = JsonConvert.SerializeObject(data);
//                    await clientremove.PostAsync("dev/update_security_group", new StringContent(dataRemove, Encoding.UTF8, "application/json"));

//                    v.IsStarted = 0;
//                    v.MachineStatus = "Deallocated";
//                    v.MachineName = "UI" + ml.UserId.ToString() + "VE" + v.VEProfileId;
//                    v.RunningBy = 0;
//                    v.IpAddress = null;
//                    _db.Entry(v).State = EntityState.Modified;
//                    _db.SaveChanges();
//                    mls.Logs = "(Deallocated)" + dateUtc + "---" + mls.Logs;
//                    mls.LastStatus = "Deallocated";
//                    mls.ModifiedDate = dateUtc;

//                    _db.SaveChanges();
//                }
//                if (isRunning.ToLower() == "stopping" && ml.IsStarted != 0)
//                {

//                    var v = _db.MachineLabs.Where(q => q.ResourceId == ml.ResourceId).FirstOrDefault();

//                    HttpClient clientremove = new HttpClient();

//                    clientremove.BaseAddress = new Uri(AWSVM);
//                    clientremove.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

//                    var data = new
//                    {
//                        instance_id = v.ResourceId,
//                        ip_address = v.IpAddress,
//                        region = "ap-southeast-1",
//                        action_type = "REMOVE"
//                    };

//                    var dataRemove = JsonConvert.SerializeObject(data);
//                    await clientremove.PostAsync("dev/update_security_group", new StringContent(dataRemove, Encoding.UTF8, "application/json"));

//                    v.IsStarted = 2;
//                    v.MachineStatus = "Deallocating";
//                    v.MachineName = "UI" + ml.UserId.ToString() + "VE" + v.VEProfileId;
//                    v.RunningBy = 0;
//                    v.IpAddress = null;
//                    _db.Entry(v).State = EntityState.Modified;
//                    _db.SaveChanges();
//                    mls.Logs = "(Deallocated)" + dateUtc + "---" + mls.Logs;
//                    mls.LastStatus = "Deallocated";
//                    mls.ModifiedDate = dateUtc;

//                    _db.SaveChanges();
//                }


//            }
//            catch (Exception e)
//            {
//                log.LogInformation($"Error Updating AWS: {e.Message}");
//            }
//        }

//    }
//}
