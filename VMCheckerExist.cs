using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Host;
using Microsoft.Extensions.Logging;
using VMWAProvision.Controller;
using VMWAProvision.Models;
using static VMWAProvision.Helpers.Helper;
using static VMWAProvision.Helpers.AzureAz;

namespace VMWAProvision
{
    public class VMCheckerExist
    {
        [FunctionName("VMCheckerExist")]
        public static async Task Run([TimerTrigger("* * * * * *")] TimerInfo myTimer, ILogger log)
        {
            log.LogInformation($"Start VMCheckerExist");

            CSDBContext _db = new CSDBContext();
            CSDBTenantContext _dbTenant = new CSDBTenantContext();
            CSDBCustomerVMContext _dbCustomer = new CSDBCustomerVMContext();

            VMOperation check = new VMOperation();

            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;

            var data = _db.MachineLabs.Where(q => q.IsStarted != 3).Join(_db.CloudLabUsers, a => a.UserId, b => b.UserId, (a, b) => new { a, b })
                .Join(_db.VEProfiles, c => c.a.VEProfileId, d => d.VEProfileID, (c, d) => new { c, d })
                .Join(_db.VirtualEnvironments, e => e.d.VirtualEnvironmentID, f => f.VirtualEnvironmentID, (e, f) => new { e, f })
                .Select(w => new { ml = w.e.c.a, TenantId = w.e.c.b.TenantId, w.f.VETypeID, w.e.c.a.IsStarted }).ToList();

            var _azure = Az(log);

            foreach (var item in data)
            {
                if (DateTime.UtcNow.Subtract(Convert.ToDateTime(item.ml.DateProvision)).TotalHours >= 12 && item.IsStarted != 7)
                {
                    if (item.IsStarted != 3)
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
                            var customer = _dbCustomer.VirtualMachineDetails.Where(q => q.ResourceId == item.ml.ResourceId).FirstOrDefault();

                            if (item.VETypeID <= 4) //AZURE
                            {
                                var isExist = check.CheckVMAzure(ml, _azure);
                                if (!isExist)
                                    UpdateVMNotExist(ml);
                            }
                            else if (item.VETypeID == 9) //AWS
                            {
                                var isExist = check.CheckVMAWS(ml, log).Result;
                                if (!isExist)
                                    UpdateVMNotExist(ml);
                            }
                            else if (item.VETypeID == 10) //GCP
                            {
                                var isExist = check.CheckVMGCP(ml);
                                if (!isExist)
                                    UpdateVMNotExist(ml);
                            }
                        });
                    }
                }
            }
        }
    }
}
