//using System;
//using System.Linq;
//using System.Threading.Tasks;
//using Microsoft.Azure.WebJobs;
//using Microsoft.Azure.WebJobs.Host;
//using Microsoft.Extensions.Logging;
//using VMWAProvision.Controller;
//using VMWAProvision.Models;
//using static VMWAProvision.Helpers.Helper;
//using static VMWAProvision.Helpers.AzureAz;
//using System.Runtime.Intrinsics.X86;
//using System.Security.Cryptography;
//using System.IO;
//using System.Text;
//using Microsoft.Azure.WebJobs.Extensions.Http;
//using System.Net.Http;

//namespace VMWAProvision
//{
//    public static class UpdateAzureVersion1
//    {
//        [FunctionName("UpdateAzureVersion1")]
//        public static async Task Run([HttpTrigger(AuthorizationLevel.Function, "get", "post", Route = null)] HttpRequestMessage req, ILogger log)
//        // public static async Task Run([TimerTrigger("0 */3 * * * *")]TimerInfo myTimer, ILogger log)
//        {
//            try
//            {
//                log.LogInformation($"Start UpdateAzureVersion1");

//                CSDBContext _db = new CSDBContext();
//                CSDBTenantContext _dbTenant = new CSDBTenantContext();
//                CSDBCustomerVMContext _dbCustomer = new CSDBCustomerVMContext();
//                VMOperationVersion1 deallocate = new VMOperationVersion1();

//                var data = _db.MachineLabs.Where(q => q.IsStarted == 4).Join(_db.CloudLabUsers, a => a.UserId, b => b.UserId, (a, b) => new { a, b })
//                    .Join(_db.VEProfiles, c => c.a.VEProfileId, d => d.VEProfileID, (c, d) => new { c, d }).Join(_db.VirtualEnvironments, e => e.d.VirtualEnvironmentID, f => f.VirtualEnvironmentID, (e, f) => new { e, f })
//                    .Join(_db.MachineLogs, g => g.e.c.a.ResourceId, h => h.ResourceId, (g, h) => new { g, h }).Select(w => new { ml = w.g.e.c.a, TenantId = w.g.e.c.b.TenantId, w.g.f.VETypeID, w.h.ModifiedDate, w.g.e.c.a.ScheduledBy,w.g.e.c.a.IsStarted }).ToList()
//                    .Where(q=>q.ScheduledBy == "Kenneth The Great" && q.IsStarted ==4).ToList();


//                foreach (var item in data)
//                {
//                    try
//                    {
//                        await Task.Run(() =>
//                        {
//                            TenantDetails tenants = new TenantDetails();

//                            //var dataTenant = _dbTenant.AzTenants.Where(q => q.TenantId == item.TenantId).FirstOrDefault();

//                            tenants.EnvironmentCode = "P";
//                            tenants.GuacamoleURL = "https://csguacwhizhack.cloudswyft.com/";
//                            tenants.GuacConnection = "user id=guacamole_user;password=CloudSwyft2020!;server=csguacwhizhack.southeastasia.cloudapp.azure.com;port=3306;database=guacamole_db;connectiontimeout=3000;defaultcommandtimeout=3000;protocol=Socket";
//                            tenants.SubscriptionKey = "7bbdc563-fbfe-459b-9c33-5e9df78bca98";
//                            tenants.TenantId = 2;
//                            tenants.TenantKey = "841fbdba-c11c-4b74-afee-4bb3712866d7";

//                            var _azure = Az(log);
//                            var ml = _db.MachineLabs.Where(q => q.ResourceId == item.ml.ResourceId).FirstOrDefault();
//                            var customer = _dbCustomer.VirtualMachineDetails.Where(q => q.ResourceId == item.ml.ResourceId).FirstOrDefault();

//                            log.LogInformation($"ResourceId = {item.ml.ResourceId}");

//                            var vmStatus = _azure.Deployments.GetByName($"virtual-machine-{item.ml.ResourceId}".ToLower()).ProvisioningState.Value;

//                            log.LogInformation($"VMStatus = {vmStatus}");

//                            if (vmStatus == "Succeeded")
//                            {
//                                var guacURL = AddMachineToDatabase(item.ml.VMName, tenants, item.ml.Username, Decrypt(item.ml.Password), item.VETypeID, item.ml.VMName + ".southeastasia.cloudapp.azure.com", log);
//                                log.LogInformation($"GuacURL = {guacURL}");
//                                UpdateMachineDatabase(ml, guacURL, "southeastasia", log, 1);

//                                //deallocate.Deallocate(ml, log).ConfigureAwait(true);

//                                log.LogInformation($"Done UpdateMachineDatabase");

//                            }
//                            if (vmStatus == "Failed")
//                            {
//                                deallocate.DeallocateFailed(ml, log).ConfigureAwait(true);
//                                UpdateMachineStatus(ml, log, 3, "Failed");
//                                //UpdateMachineDatabaseFailed(item.ml.ResourceId, 3, 0);
//                                log.LogInformation($"FAILED: Done UpdateMachineDatabaseFailed");
//                            }

//                        });
//                    }
//                    catch (Exception e)
//                    {
//                        var ml = _db.MachineLabs.Where(q => q.ResourceId == item.ml.ResourceId).FirstOrDefault();

//                        deallocate.DeallocateFailed(ml, log).ConfigureAwait(true);
//                        UpdateMachineStatus(ml, log, 3, "Catch");
//                        //UpdateMachineDatabaseFailed(item.ml.ResourceId, 3, 0);
//                        log.LogInformation($"FAILED: Done UpdateMachineDatabaseFailed");

//                    }
//                    //if (DateTime.UtcNow.Subtract(item.ModifiedDate.Value).Minutes >= 4 && DateTime.UtcNow.Subtract(item.ModifiedDate.Value).Minutes <= 30) // if provisioning is over 4 minutes but less than 15
//                    //{

//                    //}
//                    //else if (DateTime.UtcNow.Subtract(item.ModifiedDate.Value).Minutes > 16)
//                    //{
//                    //    var ml = _db.MachineLabs.Where(q => q.ResourceId == item.ml.ResourceId).FirstOrDefault();

//                    //    UpdateMachineStatus(ml, log, 3, "Too long to provision");
//                    //}
//                }
//                //return new OkObjectResult("tapos na");
//            }
//            catch (Exception ex)
//            {
//                //return new OkObjectResult(ex.Message);

//            }
//        }

//        public static string Decrypt(string cipherText)
//        {
//            string EncryptionKey = "abc123";
//            cipherText = cipherText.Replace(" ", "+");
//            byte[] cipherBytes = Convert.FromBase64String(cipherText);
//            using (System.Security.Cryptography.Aes encryptor = System.Security.Cryptography.Aes.Create())
//            {
//                Rfc2898DeriveBytes pdb = new Rfc2898DeriveBytes(EncryptionKey, new byte[] { 0x49, 0x76, 0x61, 0x6e, 0x20, 0x4d, 0x65, 0x64, 0x76, 0x65, 0x64, 0x65, 0x76 });
//                encryptor.Key = pdb.GetBytes(32);
//                encryptor.IV = pdb.GetBytes(16);
//                using (MemoryStream ms = new MemoryStream())
//                {
//                    using (CryptoStream cs = new CryptoStream(ms, encryptor.CreateDecryptor(), CryptoStreamMode.Write))
//                    {
//                        cs.Write(cipherBytes, 0, cipherBytes.Length);
//                        cs.Close();
//                    }
//                    cipherText = Encoding.Unicode.GetString(ms.ToArray());
//                }
//            }
//            return cipherText;
//        }

//    }
//}
