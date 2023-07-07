using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Azure.WebJobs.Host;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using VMWAProvision.Controller;
using VMWAProvision.Model;
using VMWAProvision.Models;
using static VMWAProvision.Helpers.Helper;
//using static VMWAProvision.Helpers.GuestAzureWindowsHelper;
using System.Threading;

namespace VMWAProvision
{
    public class VMUpdateGCP
    {
        public static string GCP = GetEnvironmentVariable("GCP");

        [FunctionName("VMUpdateGCP")]
        public async Task Run([TimerTrigger("0 * * * * *")] TimerInfo myTimer, ILogger log)       
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

                var data = _db.MachineLabs.Where(q => q.IsStarted == 4).Join(_db.CloudLabUsers, a => a.UserId, b => b.UserId, (a, b) => new { a, b })
                    .Join(_db.VEProfiles, c => c.a.VEProfileId, d => d.VEProfileID, (c, d) => new { c, d }).Join(_db.VirtualEnvironments, e => e.d.VirtualEnvironmentID, f => f.VirtualEnvironmentID, (e, f) => new { e, f })
                   .Where(j => j.f.VETypeID == 10).Select(w => new { ml = w.e.c.a, TenantId = w.e.c.b.TenantId, w.f.VETypeID }).ToList();

                var sss = DateTime.UtcNow;
                var dataDate = new DateTime();
                
                foreach (var item in data)
                {

                    if (_db.MachineLogs.Any(w => w.ResourceId == item.ml.ResourceId))
                    {
                        var mod = _db.MachineLogs.Where(w => w.ResourceId == item.ml.ResourceId).FirstOrDefault();
                        dataDate = mod.ModifiedDate.Value;
                    }
                    else
                        dataDate = item.ml.DateProvision; //DateTime.ParseExact(item.ml.DateProvision, "M/d/yyyy h:mm:ss tt", CultureInfo.InvariantCulture);

                    if (DateTime.UtcNow.Subtract(dataDate).TotalMinutes >= 10 && DateTime.UtcNow.Subtract(dataDate).TotalMinutes <= 60) // if provisioning is over 4 minutes but less than 15
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

                            if (data.data.vm_pass != "PROVISIONING NOT READY")
                            {


                                //GuestAzureHelper();

                                //Thread.Sleep(120000);

                                log.LogInformation($"Password: {data.data.vm_pass}");
                                log.LogInformation($"Password = {Encrypt(data.data.vm_pass)}");
                                var guacURL = AddMachineToDatabase(ml.VMName, tenants, data.data.user, data.data.vm_pass, item.VETypeID, data.data.nat_i_p, log);

                                log.LogInformation($"GuacURL = {guacURL}");

                                ml.Password = Encrypt(data.data.vm_pass);

                                UpdateMachineGCPDBGuac(ml, guacURL, data, log);

                                deallocate.DeallocateGCP(ml, log).ConfigureAwait(true);

                                log.LogInformation($"Done UpdateMachineDatabase");

                            }

                        });

                    }
                    else if (DateTime.UtcNow.Subtract(dataDate).TotalMinutes > 61)
                    {
                        var ml = _db.MachineLabs.Where(q => q.ResourceId == item.ml.ResourceId).FirstOrDefault();

                        UpdateMachineStatus(ml, log, 3, "Too long to provision");
                    }
                }

            }
            catch (Exception ex)
            {
                log.LogInformation($"VMUpdateAzure = {ex.Message}");
            }

            log.LogInformation($"C# Timer trigger function executed at: {DateTime.Now}");
        }
        public static string Encrypt(string clearText)
        {
            string EncryptionKey = "abc123";
            byte[] clearBytes = Encoding.Unicode.GetBytes(clearText);
            using (Aes encryptor = Aes.Create())
            {
                Rfc2898DeriveBytes pdb = new Rfc2898DeriveBytes(EncryptionKey, new byte[] { 0x49, 0x76, 0x61, 0x6e, 0x20, 0x4d, 0x65, 0x64, 0x76, 0x65, 0x64, 0x65, 0x76 });
                encryptor.Key = pdb.GetBytes(32);
                encryptor.IV = pdb.GetBytes(16);
                using (MemoryStream ms = new MemoryStream())
                {
                    using (CryptoStream cs = new CryptoStream(ms, encryptor.CreateEncryptor(), CryptoStreamMode.Write))
                    {
                        cs.Write(clearBytes, 0, clearBytes.Length);
                        cs.Close();
                    }
                    clearText = Convert.ToBase64String(ms.ToArray());
                }
            }
            return clearText;
        }

    }
}
