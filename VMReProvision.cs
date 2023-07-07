using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System.Net.Http;
using VMWAProvision.Models;
using System.Linq;
using static VMWAProvision.Helpers.Helper;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using static VMWAProvision.Helpers.AzureAz;
using System.Threading;
using VMWAProvision.Controller;

namespace VMWAProvision
{
    public static class VMReProvision
    {
        [FunctionName("VMReProvision")]
        public static async Task<IActionResult> Run([HttpTrigger(AuthorizationLevel.Function, "get", "post", Route = null)] HttpRequestMessage req, ILogger log)
        {
            log.LogInformation("C# HTTP trigger function processed a request.");

            dynamic body = await req.Content.ReadAsStringAsync();
            var data = JsonConvert.DeserializeObject<VMDeleteDetails>(body as string);

            CSDBContext _db = new CSDBContext();
            CSDBTenantContext _dbTenant = new CSDBTenantContext();
            CSDBCustomerVMContext _dbCustomer = new CSDBCustomerVMContext();
            VMOperation vmOperations = new VMOperation();

            var userData = _db.CloudLabUsers.Where(q => q.UserId == data.UserId).FirstOrDefault();

            var _ml = _db.MachineLabs.Where(q => q.UserId == data.UserId && q.VEProfileId == data.VEProfileId).FirstOrDefault();
            
            bool isLoop = true;
            int count = 0;

            var userTenant = _dbTenant.AzTenants.Where(q => q.TenantId == userData.TenantId).FirstOrDefault();
            string ResourceGroup = "";

            var environ = userTenant.EnvironmentCode.Trim() == "D" ? "DEV" : userTenant.EnvironmentCode.Trim() == "Q" ? "QA" : userTenant.EnvironmentCode.Trim() == "U" ? "DMO" : "PRD";

            if (userTenant.ClientCode.Length == 5)
                ResourceGroup = "CS-" + userTenant.ClientCode + userTenant.EnvironmentCode.Trim().ToUpper() + "-RGRP";
            else if (userTenant.ClientCode.Length == 3)
                ResourceGroup = "CS-" + environ + "-" + userTenant.ClientCode;

            var storage = _db.VEProfiles.Join(_db.VirtualEnvironmentImages, a => a.VirtualEnvironmentID, b => b.VirtualEnvironmentID, (a, b) => new { a, b }).FirstOrDefault().b.Name;

            var storageAccountOfVHD = storage.Substring(storage.IndexOf("https", 0) + 8, storage.IndexOf(".blob", (storage.IndexOf("https", 0) + 8)) - (storage.IndexOf("https", 0) + 8));

            var _azure = Az(log);

            var isVMexist = _azure.VirtualMachines.List().Select(q => q.Name).ToList().Contains(data.VirtualMachines.ToUpper());

            HttpClient clientFA = new HttpClient();
            clientFA.BaseAddress = new Uri(GetEnvironmentVariable("DeleteVMURL"));
            clientFA.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;

            var payLoad = new
            {
                subscriptionId = data.SubscriptionId,
                VirtualMachines = data.VirtualMachines,
                ResourceGroupName = ResourceGroup,
                StorageAccountOfVHD = storageAccountOfVHD,//"dictcmdisk",
                StorageAccountOfVHDResourceGroup = GetEnvironmentVariable("StorageAccountOfVHDResourceGroup"),// "CS-CLOUDLABS-DICT-P-RG",
                IncludeVhd = true,
                contactEmail = data.DeletedBy,
                ClientCode = data.ClientCode,
                version = "2",
                tenantId = data.TenantId,
                ApplicationId = data.ApplicationId,
                ApplicationSecret = data.ApplicationSecret
            };

            var dataMessage = JsonConvert.SerializeObject(payLoad);

            log.LogInformation($"isVMexist: {isVMexist}");

            if (isVMexist)
            {
                log.LogInformation($"Pasok");
                await Task.Run(() =>
                {
                    log.LogInformation($"Yown");
                    clientFA.PostAsync("", new StringContent(dataMessage, Encoding.UTF8, "application/json"));

                    while (isLoop)
                    {
                        Thread.Sleep(60000);

                        //var isVMexist = _azure.VirtualMachines.List().Select(q => q.Name).ToList().Contains(data.VirtualMachines);
                        //var isVMExist = _azure.VirtualMachines.GetByResourceGroup(ResourceGroup, _ml.VMName);
                        var isVMexist = _azure.VirtualMachines.List().Select(q => q.Name).ToList().Contains(data.VirtualMachines.ToUpper());

                        if (!isVMexist)
                            isLoop = false;
                        else
                            count++;

                        if (count == 10)
                        {
                            UpdateMachineFailureToDeleteStatus(_ml, log);
                            isLoop = false;
                        }
                        else
                        {
                            if (!isLoop)
                            {
                                UpdateMachineDeleteStatus(_ml, log);
                                log.LogInformation("OKAY NA");
                                vmOperations.Provision(data.UserId, data.VEProfileId, data.NewImageName, data.DeletedBy, log).ConfigureAwait(true);
                            }
                        }
                    }

                });
            }
            else
            {
                UpdateMachineDeleteStatus(_ml, log);
                log.LogInformation("OKAY NA");
                vmOperations.Provision(data.UserId, data.VEProfileId, data.NewImageName, data.DeletedBy, log).ConfigureAwait(true);
            }


            return new OkObjectResult("");
        }
    }
}
