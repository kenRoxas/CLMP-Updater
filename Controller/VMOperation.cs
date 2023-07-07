using System;
using System.Collections.Generic;
using System.Text;
using VMWAProvision.Models;
using static VMWAProvision.Helpers.Helper;
using static VMWAProvision.Helpers.AzureAz;
using Microsoft.Extensions.Logging;
using System.Linq;
using Microsoft.Azure.Management.Compute.Fluent;
using System.Threading.Tasks;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using Newtonsoft.Json;
using System.Threading;
using Microsoft.Azure.Management.Fluent;
using Microsoft.Azure.Management.Storage.Fluent;
using System.Data.Entity;
using Newtonsoft.Json.Linq;

namespace VMWAProvision.Controller
{
    public class VMOperation
    {
        public CSDBContext _db = new CSDBContext();
        public CSDBCustomerVMContext _dbCustomer = new CSDBCustomerVMContext();
        public CSDBTenantContext _dbTenant = new CSDBTenantContext();
        public static string AWSVM = GetEnvironmentVariable("AWSVM");
        public static string GCP = GetEnvironmentVariable("GCP");

        public async Task<string> Deallocate(MachineLabs ml, ILogger log)
        {
            var _azureProd = Az(log);
            var currentDate = DateTime.UtcNow;
            var status = true;

            IVirtualMachine vmData = _azureProd.VirtualMachines.List().Where(vm => vm.Name.ToLower() == ml.VMName.ToLower()).FirstOrDefault();

            log.LogInformation("Time Difference Before " + (currentDate - DateTime.UtcNow).Minutes);

            if (vmData.PowerState.Value.Split("/")[1].ToLower() == "deallocated")
            {
                log.LogInformation("Status is Deallocated!");
                UpdateMachineStatus(ml, log, 0, "");
                status = !status;
            }
            else
                await vmData.DeallocateAsync();

            while (status)
            {
                Thread.Sleep(30000);
                IVirtualMachine vmData2 = _azureProd.VirtualMachines.List().Where(vm => vm.Name.ToLower() == ml.VMName.ToLower()).FirstOrDefault();

                if (vmData2.PowerState.Value.Split("/")[1].ToLower() == "deallocated")
                {
                    log.LogInformation("Status is Deallocated!");
                    UpdateMachineStatus(ml, log, 0, "");
                    status = !status;
                }
            }

            //  UpdateMachineStatus(ml, log, 0);


            return "";
        }

        public async Task<string> Provision(int userId, int veprofileId, string tempVMName, string schedBy, ILogger log)
        {
            try
            {
                log.LogInformation("Start ReProvision");

                HttpClient client = new HttpClient();
                client.BaseAddress = new Uri(GetEnvironmentVariable("ProvisionVMAzure"));
                client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

                ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;

                MachineLogs mls = new MachineLogs();
                DateTime dateUtc = DateTime.UtcNow;

                var veType = _db.VEProfiles.Where(q => q.VEProfileID == veprofileId).Join(_db.VirtualEnvironments,
                        a => a.VirtualEnvironmentID,
                        b => b.VirtualEnvironmentID,
                        (a, b) => new { a, b }).Join(_db.VETypes, c => c.b.VETypeID, d => d.VETypeID, (c, d) => new { c, d }).Select(q => q.d).FirstOrDefault();

                var user = _db.CloudLabUsers.Where(q => q.UserId == userId).FirstOrDefault();
                log.LogInformation($"UserId = {user.UserId}");

                var tenant = _dbTenant.AzTenants.Where(x => x.TenantId == user.TenantId).FirstOrDefault();
                log.LogInformation($"tenant Id = {tenant.TenantId}");
                var userGroup = _db.CloudLabsGroups.Where(q => q.TenantId == user.TenantId).FirstOrDefault();
                log.LogInformation($"userGroup Id = {userGroup.CloudLabsGroupID}");
                var groupPrefix = _db.CloudLabsGroups.Where(q => q.TenantId == user.TenantId).FirstOrDefault().CLPrefix;

                var VMvhdUrl = _db.VEProfiles.Where(q => q.VEProfileID == veprofileId).Select(w => new { w.VirtualEnvironmentID, w.VEProfileID }).Join(_db.VirtualEnvironmentImages,
                       a => a.VirtualEnvironmentID,
                       b => b.VirtualEnvironmentID,
                       (a, b) => new { a, b }).FirstOrDefault().b.Name;

                var environment = tenant.EnvironmentCode.Trim() == "D" ? "DEV" : tenant.EnvironmentCode.Trim() == "Q" ? "QA" : tenant.EnvironmentCode.Trim() == "U" ? "DMO" : "PRD";

                string computerName = "U" + userId + "V" + veprofileId;
                string username = GenerateUserNameRandomName();
                string password = GeneratePasswordRandomName();
                string ResourceId = Guid.NewGuid().ToString();
                string VMName = "CS-" + userGroup.CLPrefix + "-" + tenant.EnvironmentCode.Trim() + "-VM-" + "U" + userId + "V" + veprofileId + "-" + Guid.NewGuid();

                var veprofileMappings = _db.VEProfileLabCreditMappings.Where(q => q.VEProfileID == veprofileId && q.GroupID == userGroup.CloudLabsGroupID).FirstOrDefault();

                MachineLabs ml = new MachineLabs();

                object dataV = new object();
                object qCustom = new object();


                if (veType.VETypeID <= 4) //windows or linux azure 
                {
                    log.LogInformation($"veType.VETypeID = {veType.VETypeID}");
                    var storageAccountName = VMvhdUrl.Substring(VMvhdUrl.IndexOf("https://") + 8, VMvhdUrl.IndexOf(".") - VMvhdUrl.IndexOf("https://") - 8);

                    if (veType.VETypeID <= 2)
                    {
                        if (userGroup.CLPrefix.Length == 5)// version 2.2
                        {
                            dataV = new
                            {
                                SubscriptionId = tenant.SubscriptionId,
                                TenantId = tenant.ApplicationTenantId,
                                ApplicationKey = tenant.ApplicationSecretKey,
                                ApplicationId = tenant.ApplicationId,
                                Location = tenant.Regions,
                                Environment = tenant.EnvironmentCode.Trim(),
                                ClientCode = userGroup.CLPrefix,
                                VirtualMachineName = VMName,//"cs-PATTN-d-vm-U3V2-1254c6667524480ead0e5491dd6a17a8",
                                Size = veprofileMappings.MachineSize,
                                IsCustomTemplate = false,
                                TempStorageSizeInGb = 127,
                                ImageUri = VMvhdUrl,
                                ContactPerson = schedBy,
                                StorageAccountName = VMvhdUrl.Substring(VMvhdUrl.IndexOf("https", 0) + 8, VMvhdUrl.IndexOf(".blob", (VMvhdUrl.IndexOf("https", 0) + 8)) - (VMvhdUrl.IndexOf("https", 0) + 8)),
                                OsType = veType.Description,
                                ComputerName = computerName,//"cs-PATTN-d-vm-U3V2-1254c6667524480ead0e5491dd6a17a8",
                                Username = username,
                                Password = password,
                                Fqdn = userGroup.CLUrl.Substring(8, userGroup.CLUrl.Length - 8),// CLMP URL WITHOUT HTTPS userGroup.CLUrl.Split(("https://")[1].ToLower(),
                                apiprefix = userGroup.ApiPrefix,
                                uniqueId = ResourceId
                            };
                        }
                        else if (userGroup.CLPrefix.Length == 3)
                        {
                            dataV = new
                            {
                                SubscriptionId = tenant.SubscriptionId,
                                TenantId = tenant.ApplicationTenantId,
                                ApplicationKey = tenant.ApplicationSecretKey,
                                ApplicationId = tenant.ApplicationId,
                                Location = tenant.Regions,
                                Environment = environment,
                                ClientCode = userGroup.CLPrefix,
                                VirtualMachineName = VMName,//"cs-PATTN-d-vm-U3V2-1254c6667524480ead0e5491dd6a17a8",
                                Size = veprofileMappings.MachineSize,
                                IsCustomTemplate = false,
                                TempStorageSizeInGb = 127,
                                ImageUri = VMvhdUrl,
                                ContactPerson = schedBy,
                                StorageAccountName = VMvhdUrl.Substring(VMvhdUrl.IndexOf("https", 0) + 8, VMvhdUrl.IndexOf(".blob", (VMvhdUrl.IndexOf("https", 0) + 8)) - (VMvhdUrl.IndexOf("https", 0) + 8)),
                                OsType = veType.Description,
                                ComputerName = computerName,//"cs-PATTN-d-vm-U3V2-1254c6667524480ead0e5491dd6a17a8",
                                Username = username,
                                Password = password,
                                Fqdn = userGroup.CLUrl.Substring(8, userGroup.CLUrl.Length - 8),// CLMP URL WITHOUT HTTPS userGroup.CLUrl.Split(("https://")[1].ToLower(),
                                apiprefix = userGroup.ApiPrefix,
                                uniqueId = ResourceId,
                                ResourceGroupName = "CS-" + environment + "-" + userGroup.CLPrefix
                            };
                        }

                        var dataMessage = JsonConvert.SerializeObject(dataV);

                        await Task.Run(() =>
                        {
                            client.PostAsync("", new StringContent(dataMessage, Encoding.UTF8, "application/json"));
                        });
                    }
                    else
                    {
                        if (userGroup.CLPrefix.Length == 5)// version 2.2
                        {
                            qCustom = new
                            {
                                SubscriptionId = tenant.SubscriptionId,
                                TenantId = tenant.ApplicationTenantId,
                                ApplicationKey = tenant.ApplicationSecretKey,
                                ApplicationId = tenant.ApplicationId,
                                Location = tenant.Regions,
                                Environment = tenant.EnvironmentCode.Trim(),
                                ClientCode = userGroup.CLPrefix,
                                VirtualMachineName = tempVMName,
                                Size = veprofileMappings.MachineSize,
                                IsCustomTemplate = false,
                                TempStorageSizeInGb = 127,
                                ImageUri = VMvhdUrl,
                                ContactPerson = schedBy,
                                StorageAccountName = VMvhdUrl.Substring(VMvhdUrl.IndexOf("https", 0) + 8, VMvhdUrl.IndexOf(".blob", (VMvhdUrl.IndexOf("https", 0) + 8)) - (VMvhdUrl.IndexOf("https", 0) + 8)),
                                OsType = veType.Description,
                                ComputerName = computerName,
                                Username = "cloudswyft",
                                Password = "CustomPassword1!",
                                Fqdn = userGroup.CLUrl.Substring(8, userGroup.CLUrl.Length - 8),// CLMP URL WITHOUT HTTPS userGroup.CLUrl.Split(("https://")[1].ToLower(),
                                apiprefix = userGroup.ApiPrefix,
                                uniqueId = ResourceId
                            };
                        }
                        else if (userGroup.CLPrefix.Length == 3)
                        {
                            qCustom = new
                            {
                                SubscriptionId = tenant.SubscriptionId,
                                TenantId = tenant.ApplicationTenantId,
                                ApplicationKey = tenant.ApplicationSecretKey,
                                ApplicationId = tenant.ApplicationId,
                                Location = tenant.Regions,
                                Environment = tenant.EnvironmentCode.Trim(),
                                ClientCode = userGroup.CLPrefix,
                                VirtualMachineName = tempVMName,
                                Size = veprofileMappings.MachineSize,
                                IsCustomTemplate = false,
                                TempStorageSizeInGb = 127,
                                ImageUri = VMvhdUrl,
                                ContactPerson = schedBy,
                                StorageAccountName = VMvhdUrl.Substring(VMvhdUrl.IndexOf("https", 0) + 8, VMvhdUrl.IndexOf(".blob", (VMvhdUrl.IndexOf("https", 0) + 8)) - (VMvhdUrl.IndexOf("https", 0) + 8)),
                                OsType = veType.Description,
                                ComputerName = computerName,
                                Username = "cloudswyft",
                                Password = "CustomPassword1!",
                                Fqdn = userGroup.CLUrl.Substring(8, userGroup.CLUrl.Length - 8),// CLMP URL WITHOUT HTTPS userGroup.CLUrl.Split(("https://")[1].ToLower(),
                                apiprefix = userGroup.ApiPrefix,
                                uniqueId = ResourceId,
                                ResourceGroupName = "CS-" + environment + "-" + userGroup.CLPrefix

                            };
                        }

                        var dataMessage = JsonConvert.SerializeObject(qCustom);

                        await Task.Run(() =>
                        {
                            client.PostAsync("", new StringContent(dataMessage, Encoding.UTF8, "application/json"));
                        });
                    }

                    if (veType.VETypeID == 3 || veType.VETypeID == 4)
                        VMName = tempVMName;

                    ml.DateProvision = dateUtc;

                    ml.VMName = VMName;
                    ml.MachineName = computerName;
                    ml.ResourceId = ResourceId;
                    ml.UserId = userId;
                    ml.Username = username;
                    ml.Password = Encrypt(password);
                    ml.VEProfileId = veprofileId;
                    ml.IsStarted = 4;
                    ml.IsDeleted = 0;
                    ml.MachineStatus = "Provisioning";
                    ml.ScheduledBy = schedBy;

                    mls.ResourceId = ResourceId;
                    mls.LastStatus = "Provisioning";
                    mls.Logs = '(' + mls.LastStatus + ')' + dateUtc;
                    mls.ModifiedDate = dateUtc;

                    _db.MachineLogs.Add(mls);
                    _db.MachineLabs.Add(ml);
                    _db.SaveChanges();

                    VirtualMachineDetails vmDetails = new VirtualMachineDetails();
                    vmDetails.ResourceId = ml.ResourceId;
                    vmDetails.Status = ml.IsStarted;
                    vmDetails.VMName = VMName;
                    vmDetails.FQDN = VMName + "." + tenant.Regions + ".cloudapp.azure.com";
                    vmDetails.DateLastModified = DateTime.UtcNow;
                    vmDetails.DateCreated = DateTime.UtcNow;
                    vmDetails.OperationId = $"virtual-machine-{ml.ResourceId}";

                    _dbCustomer.VirtualMachineDetails.Add(vmDetails);
                    _dbCustomer.SaveChanges();


                }

                return "";
            }
            catch (Exception e)
            {
                log.LogInformation($"Error ReProvision: {e.Message}");
                return "";
            }

        }

        public async Task<string> DeallocateFailed(MachineLabs ml, ILogger log)
        {
            log.LogInformation("Enter DeallocateFailed");
            var _azureProd = Az(log);
            var currentDate = DateTime.UtcNow;

            IVirtualMachine vmData = _azureProd.VirtualMachines.List().Where(vm => vm.Name.ToLower() == ml.VMName.ToLower()).FirstOrDefault();

            log.LogInformation("Time Difference Before " + (currentDate - DateTime.UtcNow).Minutes);

            await vmData.DeallocateAsync();

            return "";
        }

        public async Task<bool> DeleteVhd(IAzure azure, string storageAccountName, string virtualMachineName, string resourceGroupName, string vhdName, ILogger log)
        {

            string storageConnectionString = string.Empty;

            List<IStorageAccount> storageAccounts = azure.StorageAccounts.List().Where(
                    sa => sa.Name.Contains(storageAccountName)
                ).ToList();
            if (storageAccounts.Count != 0)
            {
                storageConnectionString = $"DefaultEndpointsProtocol=https;AccountName={storageAccountName};AccountKey={storageAccounts[0].GetKeys()[0].Value};EndpointSuffix=core.windows.net";
                
                log.LogInformation($"VHD: {vhdName}");
                    
                Azure.Storage.Blobs.BlobClient blobClient = new Azure.Storage.Blobs.BlobClient(storageConnectionString, "vhds", vhdName);
                if (blobClient.Exists() && blobClient.GetProperties().Value.LeaseState == Azure.Storage.Blobs.Models.LeaseState.Available)
                {
                    blobClient.DeleteIfExists(); //uncomment to delete vhd
                    log.LogInformation("VHD Deleted");
                    return true;
                }
                else
                {
                    log.LogInformation($"FAILED TO DELETE VHD: LEASE STATE {blobClient.GetProperties().Value.LeaseState}");
                    return false;
                }

            }
            else
            {
                log.LogInformation($"STORAGE ACCOUNT NOT FOUND: {storageAccountName}");
                return false;
            }
        }

        public async Task<string> DeallocateGCP(MachineLabs ml, ILogger log)
        {
            var currentDate = DateTime.UtcNow;
            var status = true;

            ml.MachineStatus = "Deallocating";
            ml.IsStarted = 2;
            ml.RunningBy = 0;

            var logs = _db.MachineLogs.Where(q => q.ResourceId == ml.ResourceId).FirstOrDefault();

            logs.Logs = "(Deallocating)" + currentDate + "---" + logs.Logs;
            logs.LastStatus = "Deallocating";
            logs.ModifiedDate = currentDate;

            _db.Entry(logs).State = EntityState.Modified;

            _db.Entry(ml).State = EntityState.Modified;
            _db.SaveChanges();


            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;

            HttpClient clientGCP = new HttpClient();
            clientGCP.BaseAddress = new Uri(GCP);
            clientGCP.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            await Task.Run(() =>
            {
                clientGCP.GetAsync("api/gcp/virtual-machine/" + ml.VMName.ToLower() + "/stop/");
            });



            return "";
        }

        public bool CheckVMAzure(MachineLabs ml, IAzure az)
        {
            return az.VirtualMachines.List().Any(vm => vm.Name.ToLower() == ml.VMName.ToLower());
        }
        public async Task<bool> CheckVMAWS(MachineLabs ml, ILogger log)
        {
            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;

            HttpClient clientAWS = new HttpClient();
            clientAWS.BaseAddress = new Uri(AWSVM);
            clientAWS.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            var jsonDetails = new
            {
                instance_id = ml.ResourceId,
                region = "ap-southeast-1"
            };
            var jsonData = JsonConvert.SerializeObject(jsonDetails);

            var responseGetDetails = await clientAWS.PostAsync("dev/get_vm_details", new StringContent(jsonData, Encoding.UTF8, "application/json"));
            var getDetails = JObject.Parse(responseGetDetails.Content.ReadAsStringAsync().Result);

            var isInstanceExists = getDetails.SelectToken("Reservations[0].Instances[0]") == null;

            if (responseGetDetails.StatusCode != HttpStatusCode.OK || isInstanceExists)
                return false;
            else
                return true;
        }

        public bool CheckVMGCP(MachineLabs ml)
        {
            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;

            HttpClient clientGCP = new HttpClient();
            clientGCP.BaseAddress = new Uri(GCP);
            clientGCP.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));


            var response = clientGCP.GetAsync("api/gcp/virtual-machine/" + ml.VMName.ToLower()).Result;

            if (response.StatusCode != HttpStatusCode.Accepted)
                return false;
            else
                return true;
        }
    }
}
