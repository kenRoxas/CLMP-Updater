using System;
using System.Data.Entity;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Host;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using VMWAProvision.Controller;
using VMWAProvision.Model;
using VMWAProvision.Models;
using static VMWAProvision.Helpers.Helper;

namespace VMWAProvision
{
    public class VMHeartBeatGCP
    {
        public static string GCP = GetEnvironmentVariable("GCP");
        public string token = "";

        [FunctionName("VMHeartBeatGCP")]
        public async Task Run([TimerTrigger("0 * * * * *")] TimerInfo myTimer, ILogger log)
        {
            try
            {
                log.LogInformation($"Start VMHeartBeatGCP");

                CSDBContext _db = new CSDBContext();
                CSDBTenantContext _dbTenant = new CSDBTenantContext();
                CSDBCustomerVMContext _dbCustomer = new CSDBCustomerVMContext();
                VMOperation deallocate = new VMOperation();

                ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;

                HttpClient clientGCP = new HttpClient();
                clientGCP.BaseAddress = new Uri(GCP);
                clientGCP.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

                var data = _db.MachineLabs.Join(_db.CloudLabUsers, a => a.UserId, b => b.UserId, (a, b) => new { a, b })
                    .Join(_db.VEProfiles, c => c.a.VEProfileId, d => d.VEProfileID, (c, d) => new { c, d }).Join(_db.VirtualEnvironments, e => e.d.VirtualEnvironmentID, f => f.VirtualEnvironmentID, (e, f) => new { e, f })
                    .Join(_db.MachineLogs, g => g.e.c.a.ResourceId, h => h.ResourceId, (g, h) => new { g, h })
                    .Where(j => j.g.f.VETypeID == 10 && (j.g.e.c.a.IsStarted != 3 && j.g.e.c.a.IsStarted != 6 && j.g.e.c.a.IsStarted != 7))
                    .Select(w => new { ml = w.g.e.c.a, TenantId = w.g.e.c.b.TenantId, w.g.f.VETypeID, w.h.ModifiedDate }).ToList();

                foreach (var item in data)
                {                   
                    await Task.Run(() =>
                    {
                        var ml = _db.MachineLabs.Where(q => q.ResourceId == item.ml.ResourceId).FirstOrDefault();
                        
                        if(_db.CloudLabsSchedules.Any(w => w.MachineLabsId == ml.MachineLabsId))
                        {
                            var sched = _db.CloudLabsSchedules.Where(w => w.MachineLabsId == ml.MachineLabsId).FirstOrDefault();

                            var customer = _dbCustomer.VirtualMachineDetails.Where(q => q.ResourceId == item.ml.ResourceId).FirstOrDefault();

                            log.LogInformation($"VMName = {item.ml.VMName}");

                            var response = clientGCP.GetAsync("api/cms/heartbeat/" + ml.VMName.ToLower()).Result;

                            var data = JsonConvert.DeserializeObject<VMHeartBeat>(response.Content.ReadAsStringAsync().Result);

                            if (ml.RunningBy == 1)
                            {
                                var studentConsumed = data.data.minutes_rendered - (120 - Math.Floor((double)(sched.InstructorLabHours / 60)));

                                var totalRemainingMinutes = (sched.LabHoursTotal * 60) - studentConsumed;

                                log.LogInformation($"{item.ml.VMName} --- minutes_rendered = {data.data.minutes_rendered}");

                                sched.TimeRemaining = totalRemainingMinutes * 60;


                                log.LogInformation($"{item.ml.VMName} --- Remaining = { totalRemainingMinutes * 60}");

                                _db.Entry(sched).State = EntityState.Modified;

                                _db.SaveChanges();

                                if (sched.TimeRemaining <= 0 && ml.IsStarted != 0)
                                {
                                    ml.IsStarted = 2;
                                    ml.MachineStatus = "Deallocating";
                                    ml.RunningBy = 0;

                                    _db.Entry(ml).State = EntityState.Modified;
                                    _db.SaveChanges();

                                    clientGCP.GetAsync("api/gcp/virtual-machine/" + ml.VMName.ToLower() + "/stop/");

                                }
                            }
                            else if (ml.RunningBy == 2)
                            {
                                var totalRemainingInstructorMinutes = sched.InstructorLabHours - 60;

                                log.LogInformation($"{item.ml.VMName} --- minutes_rendered = {data.data.minutes_rendered}");

                                sched.InstructorLabHours = totalRemainingInstructorMinutes;

                                log.LogInformation($"{item.ml.VMName} --- Instructor remaining = { totalRemainingInstructorMinutes}");

                                _db.Entry(sched).State = EntityState.Modified;
                                _db.SaveChanges();

                                if (sched.InstructorLabHours <= 0 && ml.IsStarted != 0)
                                {
                                    ml.IsStarted = 2;
                                    ml.MachineStatus = "Deallocating";
                                    ml.RunningBy = 0;

                                    _db.Entry(ml).State = EntityState.Modified;
                                    _db.SaveChanges();

                                    clientGCP.GetAsync("api/gcp/virtual-machine/" + ml.VMName.ToLower() + "/stop/");

                                }
                            }


                        }


                    });
                }
                //return new OkObjectResult("tapos na");
            }
            catch (Exception ex)
            {
                log.LogInformation($"VMUpdateAzure = {ex.Message}");

            }

            log.LogInformation($"C# Timer trigger function executed at: {DateTime.Now}");
        }

    }
}
