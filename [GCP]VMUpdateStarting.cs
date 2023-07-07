using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
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
    public class VMUpdateStartingGCP
    {
        public static string GCP = GetEnvironmentVariable("GCP");

        [FunctionName("VMUpdateStartingGCP")]
        public static async Task Run([TimerTrigger("0 * * * * *")] TimerInfo myTimer, ILogger log)
        {
            try
            {
                log.LogInformation($"Start VMUpdateGCP");

                CSDBContext _db = new CSDBContext();
                CSDBTenantContext _dbTenant = new CSDBTenantContext();
                CSDBCustomerVMContext _dbCustomer = new CSDBCustomerVMContext();
                VMOperation deallocate = new VMOperation();
                
                ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;

                HttpClient clientGCP = new HttpClient();
                clientGCP.BaseAddress = new Uri(GCP);
                clientGCP.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

                var data = _db.MachineLabs.Where(q => q.IsStarted == 5).Join(_db.CloudLabUsers, a => a.UserId, b => b.UserId, (a, b) => new { a, b })
                    .Join(_db.VEProfiles, c => c.a.VEProfileId, d => d.VEProfileID, (c, d) => new { c, d }).Join(_db.VirtualEnvironments, e => e.d.VirtualEnvironmentID, f => f.VirtualEnvironmentID, (e, f) => new { e, f })
                    .Join(_db.MachineLogs, g => g.e.c.a.ResourceId, h => h.ResourceId, (g, h) => new { g, h }).Where(j => j.g.f.VETypeID == 10).Select(w => new { ml = w.g.e.c.a, TenantId = w.g.e.c.b.TenantId, w.g.f.VETypeID, w.h.ModifiedDate }).ToList();

                var sss = DateTime.UtcNow;

                foreach (var item in data)
                {
                    if (DateTime.UtcNow.Subtract(item.ModifiedDate.Value).TotalMinutes >= 2 && DateTime.UtcNow.Subtract(item.ModifiedDate.Value).TotalMinutes <= 60) // if provisioning is over 4 minutes but less than 15
                    {
                        await Task.Run(() =>
                        {
                            var ml = _db.MachineLabs.Where(q => q.ResourceId == item.ml.ResourceId).FirstOrDefault();
                            var customer = _dbCustomer.VirtualMachineDetails.Where(q => q.ResourceId == item.ml.ResourceId).FirstOrDefault();

                            var tenants = _dbTenant.AzTenants.Where(q => q.TenantId == item.TenantId).Select(w => new TenantDetails
                            {
                                EnvironmentCode = w.EnvironmentCode,
                                GuacamoleURL = w.GuacamoleURL,
                                GuacConnection = w.GuacConnection,
                                SubscriptionKey = w.SubscriptionId,
                                TenantId = w.TenantId,
                                TenantKey = w.ApplicationTenantId
                            }).FirstOrDefault();

                            log.LogInformation($"VMName = {item.ml.VMName}");

                            var response = clientGCP.GetAsync("api/gcp/virtual-machine/" + ml.VMName.ToLower()).Result;

                            var data = JsonConvert.DeserializeObject<VMPayload>(response.Content.ReadAsStringAsync().Result);

                            if (data.data.status.ToLower() == "started" && data.data.vm_pass != "NOT READY" && data.data.nat_i_p != null)
                                UpdateMachineGCP(ml, log, "RUNNING", data, tenants);
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                log.LogInformation($"VMUpdateAzure = {ex.Message}");
            }

            log.LogInformation($"C# Timer trigger function executed at: {DateTime.Now}");
        }
    }
}
