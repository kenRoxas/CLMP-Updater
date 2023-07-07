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
using System.Net.Http.Headers;
using System.Net.Http;
using Newtonsoft.Json;
using System.Collections.Generic;

namespace VMWAProvision
{
    public static class VMUpdateAzure
    {
        public static string IPFirewall = GetEnvironmentVariable("IPFirewall");

        public static string ClientId =   GetEnvironmentVariable("ClientId");
        public static string ClientSecret =   GetEnvironmentVariable("ClientSecret");
        public static string TenantId = GetEnvironmentVariable("TenantId");
        public static string SubscriptionId = GetEnvironmentVariable("SubscriptionId");

        [FunctionName("VMUpdateAzure")]
        public static async Task Run5([TimerTrigger("%CRON_TIME%")] TimerInfo myTimer, ILogger log)
        {
            try
            {
                log.LogInformation($"Start VMUpdateAzure");

                CSDBContext _db = new CSDBContext();
                CSDBTenantContext _dbTenant = new CSDBTenantContext();
                CSDBCustomerVMContext _dbCustomer = new CSDBCustomerVMContext();
                VMOperation deallocate = new VMOperation();
                List<IpFirewall> ipFirewalls = new List<IpFirewall>();

                string clientCode = "";
                string envi = "";

                HttpClient client = new HttpClient();
                client.BaseAddress = new Uri(IPFirewall);
                client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

                var data = _db.MachineLabs.Where(q => q.IsStarted == 4).Join(_db.CloudLabUsers, a => a.UserId, b => b.UserId, (a, b) => new { a, b })
                    .Join(_db.VEProfiles, c => c.a.VEProfileId, d => d.VEProfileID, (c, d) => new { c, d }).Join(_db.VirtualEnvironments, e => e.d.VirtualEnvironmentID, f => f.VirtualEnvironmentID, (e, f) => new { e, f })
                    .Join(_db.MachineLogs, g => g.e.c.a.ResourceId, h => h.ResourceId, (g, h) => new { g, h }).Where(j => j.g.f.VETypeID <= 4).Select(w => new { ml = w.g.e.c.a, TenantId = w.g.e.c.b.TenantId, w.g.f.VETypeID, w.h.ModifiedDate }).ToList();
                
                foreach (var item in data)
                {
                    if (DateTime.UtcNow.Subtract(item.ModifiedDate.Value).TotalMinutes >= 4 && DateTime.UtcNow.Subtract(item.ModifiedDate.Value).TotalMinutes <= 60) // if provisioning is over 4 minutes but less than 15
                    {
                        try
                        {
                            await Task.Run(() =>
                            {
                                TenantDetails tenants = new TenantDetails();

                                var dataTenant = _dbTenant.AzTenants.Where(q => q.TenantId == item.TenantId).FirstOrDefault();
                                
                                clientCode = dataTenant.ClientCode;
                                envi = dataTenant.EnvironmentCode;

                                tenants.EnvironmentCode = dataTenant.EnvironmentCode;
                                tenants.GuacamoleURL = dataTenant.GuacamoleURL;
                                tenants.GuacConnection = dataTenant.GuacConnection;
                                tenants.SubscriptionKey = dataTenant.SubscriptionId;
                                tenants.TenantId = dataTenant.TenantId;
                                tenants.TenantKey = dataTenant.ApplicationTenantId;

                                var _azure = Az(log);
                                var ml = _db.MachineLabs.Where(q => q.ResourceId == item.ml.ResourceId).FirstOrDefault();
                                //var customer = _dbCustomer.VirtualMachineDetails.Where(q => q.ResourceId == item.ml.ResourceId).FirstOrDefault();

                                log.LogInformation($"ResourceId = {item.ml.ResourceId}");

                                var vmStatus = _azure.Deployments.GetByName($"virtual-machine-{item.ml.ResourceId}".ToLower()).ProvisioningState.Value;

                                //var vm = _azure.VirtualMachines.List().Where(vm => vm.Name.ToLower() == ml.VMName.ToLower()).FirstOrDefault();

                                //var privateIp = vm.GetPrimaryNetworkInterface().PrimaryPrivateIP;

                                //var ip = vm.GetPrimaryPublicIPAddress().IPAddress;


                                log.LogInformation($"VMStatus = {vmStatus}");

                                var data = new
                                {
                                    SubscriptionId = dataTenant.SubscriptionId,
                                    TenantId = dataTenant.ApplicationTenantId,
                                    ApplicationId = dataTenant.ApplicationId,
                                    ApplicationKey = dataTenant.ApplicationSecretKey,
                                    ClientCode = dataTenant.ClientCode,
                                    Environment = dataTenant.EnvironmentCode,
                                    vmName = item.ml.VMName
                                };

                                var dataMsg = JsonConvert.SerializeObject(data);

                                if (vmStatus == "Succeeded")
                                {
                                    if (dataTenant.IsFireWall)
                                    {
                                        //var ip = _azure.PublicIPAddresses.List().Where(q => q.Name.ToLower() == ml.VMName.ToLower()).FirstOrDefault();

                                        var ipFire = new IpFirewall();

                                        ipFire.VMName = item.ml.VMName.ToLower();

                                        ipFirewalls.Add(ipFire);
                                    }

                                    log.LogInformation($"{item.ml.Password}");
                                    log.LogInformation($"Password = {Decrypt(item.ml.Password)}");
                                    var guacURL = AddMachineToDatabase(item.ml.VMName, tenants, item.ml.Username, Decrypt(item.ml.Password), item.VETypeID, item.ml.VMName + "." + dataTenant.Regions + ".cloudapp.azure.com", log);
                                    log.LogInformation($"GuacURL = {guacURL}");
                                    UpdateMachineDatabase(ml, guacURL, dataTenant.Regions, log, 4);

                                    deallocate.Deallocate(ml, log).ConfigureAwait(true);

                                    log.LogInformation($"Done UpdateMachineDatabase ResourceId = {ml.ResourceId}");

                                }
                                if (vmStatus == "Failed")
                                {
                                    deallocate.DeallocateFailed(ml, log).ConfigureAwait(true);
                                    UpdateMachineStatus(ml, log, 3, "Failed");
                                    //UpdateMachineDatabaseFailed(item.ml.ResourceId, 3, 0);
                                    log.LogInformation($"FAILED: Done UpdateMachineDatabaseFailed");
                                }

                            });

                        }
                        catch (Exception e)
                        { 
                        
                        }

                    }
                    else if (DateTime.UtcNow.Subtract(item.ModifiedDate.Value).TotalMinutes > 61)
                    {
                        var ml = _db.MachineLabs.Where(q => q.ResourceId == item.ml.ResourceId).FirstOrDefault();

                        UpdateMachineStatus(ml, log, 3, "Too long to provision");
                    }
                }

                if(ipFirewalls.Count > 0)
                {
                    var firewallParam = new
                    {
                        SubscriptionId = SubscriptionId,
                        TenantId = TenantId,
                        ApplicationId = ClientId,
                        ApplicationKey = ClientSecret,
                        ClientCode = clientCode,
                        Environment = envi,
                        VirtualMachines = ipFirewalls
                    };

                    var dataMsg = JsonConvert.SerializeObject(firewallParam);

                    client.PostAsync("", new StringContent(dataMsg, Encoding.UTF8, "application/json"));

                    ipFirewalls.Clear();
                }
                //return new OkObjectResult("tapos na");
            }
            catch (Exception ex)
            {
                log.LogInformation($"VMUpdateAzure = {ex.Message}");

            }

        }

        public static string Decrypt(string cipherText)
        {
            string EncryptionKey = "abc123";
            cipherText = cipherText.Replace(" ", "+");
            byte[] cipherBytes = Convert.FromBase64String(cipherText);
            using (Aes encryptor = Aes.Create())
            {
                Rfc2898DeriveBytes pdb = new Rfc2898DeriveBytes(EncryptionKey, new byte[] { 0x49, 0x76, 0x61, 0x6e, 0x20, 0x4d, 0x65, 0x64, 0x76, 0x65, 0x64, 0x65, 0x76 });
                encryptor.Key = pdb.GetBytes(32);
                encryptor.IV = pdb.GetBytes(16);
                using (MemoryStream ms = new MemoryStream())
                {
                    using (CryptoStream cs = new CryptoStream(ms, encryptor.CreateDecryptor(), CryptoStreamMode.Write))
                    {
                        cs.Write(cipherBytes, 0, cipherBytes.Length);
                        cs.Close();
                    }
                    cipherText = Encoding.Unicode.GetString(ms.ToArray());
                }
            }
            return cipherText;
        }

    }
}
