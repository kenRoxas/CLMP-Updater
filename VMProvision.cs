//using System;
//using System.IO;
//using System.Threading.Tasks;
//using Microsoft.AspNetCore.Mvc;
//using Microsoft.Azure.WebJobs;
//using Microsoft.Azure.WebJobs.Extensions.Http;
//using Microsoft.AspNetCore.Http;
//using Microsoft.Extensions.Logging;
//using Newtonsoft.Json;
//using System.Net.Http;
//using static VMWAProvision.Helpers.Helper;
//using static VMWAProvision.Helpers.AzureAz;
//using Microsoft.Azure.Management.ResourceManager.Fluent.Core;
//using VMWAProvision.Models;
//using Microsoft.Azure.Management.ResourceManager.Fluent.Authentication;
//using System.Linq;
//using Microsoft.Azure.Management.ResourceManager.Fluent;
//using Microsoft.Azure.Management.Fluent;
//using Microsoft.Azure.Management.Compute.Fluent;
//using Microsoft.Azure.Management.AppService.Fluent.Models;
//using System.Diagnostics;
//using System.Threading;
//using System.Collections.Generic;
//using static VMWAProvision.EnvironmentVariableVersion2;
//using Microsoft.Azure.Management.Network.Fluent;
//using Newtonsoft.Json.Linq;

//namespace VMWAProvision
//{
//    public static class VMProvision
//    {
//        [FunctionName("VMProvision")]
//        public static async Task<IActionResult> Run([HttpTrigger(AuthorizationLevel.Function, "get", "post", Route = null)] HttpRequestMessage req, ILogger log)
//        {
//            log.LogInformation("C# HTTP trigger function processed a request.");

//            // var operationId = Activity.Current.RootId;
//            var operationId = "";

//            dynamic body = await req.Content.ReadAsStringAsync();
//            var vmProv = JsonConvert.DeserializeObject<ProvisionDetailsVM>(body as string);

//            log.LogInformation($"RG: {vmProv.ResourceGroup}");
//            log.LogInformation($"MachineName: {vmProv.MachineName}");
//            log.LogInformation($"Image VHD: {vmProv.ImageName}");
//            log.LogInformation($"Size: {vmProv.Size}");
//            log.LogInformation($"Username: {vmProv.Username}");
//            log.LogInformation($"Password: {vmProv.Password}");

//            var deallocateWhenFinish = true;
//            var region = Location(GetEnvironmentVariable("Region"));
//            log.LogInformation($"Region: {region}");

//            CSDBTenantContext _dbTenant = new CSDBTenantContext();
//            CSDBContext _db = new CSDBContext();

//            var tenants = _dbTenant.AzTenants.Where(q => q.TenantId == vmProv.TenantID).Select(w => new TenantDetails
//            {
//                TenantId = w.TenantId,
//                TenantKey = w.TenantKey,
//                SubscriptionKey = w.SubscriptionKey,
//                GuacConnection = w.GuacConnection,
//                GuacamoleURL = w.GuacamoleURL,
//                EnvironmentCode = w.EnvironmentCode
//            }).FirstOrDefault();

//            var environ = tenants.EnvironmentCode.Trim() == "D" ? "DEV" : tenants.EnvironmentCode.Trim() == "Q" ? "QA" : tenants.EnvironmentCode.Trim() == "U" ? "DMO" : "PRD";
//            var userGroups = _db.CloudLabsGroups.Where(w => w.TenantId == vmProv.TenantID).FirstOrDefault();
//            var imageURI = _db.VEProfiles.Where(q => q.VEProfileID == vmProv.VEProfileID).Join(_db.VirtualEnvironmentImages, a => a.VirtualEnvironmentID, b => b.VirtualEnvironmentID, (a, b) => new { a, b }).FirstOrDefault().b.Name;
//            var isLoop = true;
//            string createEnvironmentVariablesPsUrl = GetEnvironmentVariable("CreateEnvironmentVariablesPsUrl");

//            await Task.Run(() =>
//            {
//                try
//                {
//                    string uniqueId = Guid.NewGuid().ToString().Replace("-", "");

//                    log.LogInformation($"subscriptionId: {GetEnvironmentVariable("SubscriptionId")}");
//                    log.LogInformation($"tenantId: {GetEnvironmentVariable("TenantId")}");
//                    log.LogInformation($"applicationId: {GetEnvironmentVariable("ClientId")}");
//                    log.LogInformation($"environment: {environ}");
//                    log.LogInformation($"clientCode: {vmProv.CLPrefix}");
//                    log.LogInformation($"location: {GetEnvironmentVariable("Region")}");
//                    log.LogInformation($"virtualMachineName: {vmProv.MachineName}");
//                    log.LogInformation($"size: {vmProv.Size}");
//                    log.LogInformation($"tempStorageSizeInGb: 127");
//                    log.LogInformation($"imageUri: {imageURI}");
//                    log.LogInformation($"storageAccountName: {GetEnvironmentVariable("StorageAccountName")}");
//                    log.LogInformation($"osType: WINDOWS");
//                    log.LogInformation($"computerName: {vmProv.MachineName}");
//                    log.LogInformation($"username: {vmProv.Username}");
//                    log.LogInformation($"password: {vmProv.Password}");

//                    var _azure = Az(log);

//                    log.LogInformation(_azure.GetCurrentSubscription().SubscriptionId);

//                    log.LogInformation("Getting labs dependencies");
//                    //string resourceGroupName = $"cs-{clientCode}-{environment}-rgrp".ToUpper();
//                    //string networkSecurityGroupName = $"cs-{environment}-{clientCode}-nsg".ToUpper();
//                    //string virtualNetworkName = $"cs-{environment}-{clientCode}-vnet".ToUpper();
//                    string networkSecurityGroupName = $"{vmProv.ResourceGroup}-nsg";
//                    string virtualNetworkName = $"{vmProv.ResourceGroup}-vnet";

//                    log.LogInformation("Setting up tags");

//                    object rgTags = new
//                    {
//                        _business_name = "cs",
//                        _azure_region = GetEnvironmentVariable("Region"),
//                        _contact_person = vmProv.ScheduledBy,
//                        _client_code = vmProv.CLPrefix,
//                        _environment = environ,
//                        _lab_type = "virtualmachine",
//                        _created = DateTime.Now.ToShortDateString(),
//                    };


//                    log.LogInformation("Getting labs resource dependencies");
//                    log.LogInformation($"RG : {vmProv.ResourceGroup}");
//                    log.LogInformation($"VNet: {virtualNetworkName}");
//                    log.LogInformation($"NetSec: {networkSecurityGroupName}");

//                    string vnetId = _azure.Networks.GetByResourceGroup(vmProv.ResourceGroup, virtualNetworkName).Id.ToString();
//                    string nsgId = _azure.NetworkSecurityGroups.GetByResourceGroup(vmProv.ResourceGroup, networkSecurityGroupName).Id.ToString();
//                    INetwork virtualNetwork = _azure.Networks.GetById(vnetId);

//                    log.LogInformation($"vnetId: {vnetId}");
//                    string publicIpAddressType = virtualNetwork.Inner.Subnets[0].NatGateway == null ? "Dynamic" : "Static";
//                    string publicIpAddressSku = virtualNetwork.Inner.Subnets[0].NatGateway == null ? "Basic" : "Standard";

//                    log.LogInformation($"publicIpAddressType: {publicIpAddressType}");
//                    log.LogInformation($"publicIpAddressSku: {publicIpAddressSku}");
//                    log.LogInformation("Getting labs resource group name");

//                    //string labsResourceGroupName = await SetResourceGroupAsync(_azure, credentials, subscriptionId, location, environment, clientCode, contactPerson);
//                    string labsResourceGroupName = _azure.ResourceGroups.GetByName(vmProv.ResourceGroup).Name;

//                    Stream stream = new MemoryStream(Properties.Resources.azuredeploywindows);
//                    JObject templateParameterObjectVirtualMachine = new JObject();
//                    StreamReader template = new StreamReader(stream);
//                    JsonTextReader reader = new JsonTextReader(template);

//                    templateParameterObjectVirtualMachine = (JObject)JToken.ReadFrom(reader);

//                    templateParameterObjectVirtualMachine.SelectToken("parameters.location")["defaultValue"] = GetEnvironmentVariable("Region");
//                    templateParameterObjectVirtualMachine.SelectToken("parameters.networkSecurityGroupId")["defaultValue"] = nsgId;
//                    templateParameterObjectVirtualMachine.SelectToken("parameters.subnetName")["defaultValue"] = "default";
//                    templateParameterObjectVirtualMachine.SelectToken("parameters.virtualNetworkId")["defaultValue"] = vnetId;
//                    templateParameterObjectVirtualMachine.SelectToken("parameters.virtualMachineName")["defaultValue"] = vmProv.MachineName;
//                    templateParameterObjectVirtualMachine.SelectToken("parameters.computerName")["defaultValue"] = vmProv.MachineName;
//                    templateParameterObjectVirtualMachine.SelectToken("parameters.storageAccountName")["defaultValue"] = GetEnvironmentVariable("StorageAccountName");
//                    templateParameterObjectVirtualMachine.SelectToken("parameters.publicIpAddressType")["defaultValue"] = publicIpAddressType;
//                    templateParameterObjectVirtualMachine.SelectToken("parameters.publicIpAddressSku")["defaultValue"] = publicIpAddressSku;
//                    templateParameterObjectVirtualMachine.SelectToken("parameters.virtualMachineSize")["defaultValue"] = vmProv.Size;
//                    templateParameterObjectVirtualMachine.SelectToken("parameters.adminUsername")["defaultValue"] = vmProv.Username;
//                    templateParameterObjectVirtualMachine.SelectToken("parameters.adminPassword")["defaultValue"] = vmProv.Password;
//                    templateParameterObjectVirtualMachine.SelectToken("parameters.newTemplateName")["defaultValue"] = vmProv.MachineName;
//                    templateParameterObjectVirtualMachine.SelectToken("parameters.imageUri")["defaultValue"] = imageURI;
//                    templateParameterObjectVirtualMachine.SelectToken("parameters.tags")["defaultValue"] = JToken.FromObject(rgTags);
//                    templateParameterObjectVirtualMachine.SelectToken("parameters.diskSizeGB")["defaultValue"] = 127;
//                    templateParameterObjectVirtualMachine.SelectToken("parameters.fileUris")["defaultValue"] = createEnvironmentVariablesPsUrl;
//                    templateParameterObjectVirtualMachine.SelectToken("parameters.arguments")["defaultValue"] = $"-ResourceGroupName {labsResourceGroupName} -VirtualMachineName {vmProv.MachineName} -ComputerName {vmProv.MachineName} -TenantId {GetEnvironmentVariable("TenantId")} -GroupCode {userGroups.ApiPrefix} -Fqdn {userGroups.CLUrl.Split("https://")[1].ToLower()}";

//                    string deploymentName = $"virtual-machine-{uniqueId}".ToLower();
//                    log.LogInformation($"Deploying virtual-machine-{uniqueId}".ToLower());

//                    IDeployment vmDeployment = _azure.Deployments.Define(deploymentName)
//                        .WithExistingResourceGroup(labsResourceGroupName)
//                        .WithTemplate(templateParameterObjectVirtualMachine)
//                        .WithParameters("{}")
//                        .WithMode(Microsoft.Azure.Management.ResourceManager.Fluent.Models.DeploymentMode.Incremental)
//                        .Create();

//                    log.LogInformation($"Value of IDeployment =  {vmDeployment.ProvisioningState.Value}");

//                    if (vmDeployment.ProvisioningState.Value == "Failed")
//                    {
//                        log.LogInformation("End of Provisioning");
//                        log.LogError("Deployment Failed");
//                    }

//                    if (vmDeployment.ProvisioningState.Value == "Succeeded")
//                    {
//                        try
//                        {
//                            log.LogInformation($"Deallocating {vmProv.MachineName}");
//                            IVirtualMachine virtualMachine = _azure.VirtualMachines.GetByResourceGroup(labsResourceGroupName, vmProv.MachineName);
//                            virtualMachine.DeallocateAsync();
//                            log.LogInformation($"Deallocated");

//                            while (isLoop)
//                            {
//                                Thread.Sleep(120000);

//                                IVirtualMachine vmData2 = _azure.VirtualMachines.List().Where(vm => vm.Name.ToLower() == vmProv.MachineName.ToLower()).FirstOrDefault();

//                                if (vmData2.PowerState.Value.Split("/")[1].ToLower() == "deallocated")
//                                {
//                                    log.LogInformation("Status is deallocated!");

//                                    log.LogInformation($"{vmProv.MachineName}");
//                                    log.LogInformation($"{tenants}");
//                                    log.LogInformation($"{vmProv.Username}");
//                                    log.LogInformation($"{vmProv.Password}");
//                                    log.LogInformation($"{vmProv.VETypeID}");
//                                    log.LogInformation($"{vmProv.FQDN + "." + GetEnvironmentVariable("Region") + ".cloudapp.azure.com"}");

//                                    var guacURL = AddMachineToDatabase(vmProv.MachineName, tenants, vmProv.Username, vmProv.Password, vmProv.VETypeID, vmProv.FQDN + "." + GetEnvironmentVariable("Region") + ".cloudapp.azure.com", log);

//                                    UpdateMachineLabWithGuac(vmProv, guacURL, operationId, GetEnvironmentVariable("Region"), log);
//                                    isLoop = !isLoop;
//                                }
//                                else
//                                {
//                                    vmData2.Deallocate();
//                                }
//                            }

//                            log.LogInformation("End of Provisioning");
//                        }
//                        catch (Exception e)
//                        {

//                            log.LogError(e.Message);
//                            log.LogError("End of Provisioning");
//                        }

//                    }


//                    log.LogInformation("End of Provisioning");

//                }
//                catch (Exception e)
//                {
//                    log.LogError(e.Message);
//                }
//            });
//            return new OkObjectResult("tapos na");













//            //log.LogInformation("C# HTTP trigger function processed a request.");

//            //var operationId = Activity.Current.RootId;
//            ////var operationId = "";


//            //dynamic body = await req.Content.ReadAsStringAsync();
//            //var vmProv = JsonConvert.DeserializeObject<ProvisionDetailsVM>(body as string);

//            //log.LogInformation($"RG: {vmProv.ResourceGroup}");
//            //log.LogInformation($"MachineName: {vmProv.MachineName}");
//            //log.LogInformation($"Image VHD: {vmProv.ImageName}");
//            //log.LogInformation($"Size: {vmProv.Size}");
//            //log.LogInformation($"Username: {vmProv.Username}");
//            //log.LogInformation($"Password: {vmProv.Password}");
//            ////string name = req.Query["name"];

//            //try
//            //{
//            //    await Task.Run(() =>
//            //    {
//            //        log.LogInformation($"Task Run");

//            //        var isTrue = true;
//            //        var isLoop = true;
//            //        var isFailed = false;

//            //        var region = Location(GetEnvironmentVariable("Region"));
//            //        log.LogInformation($"Region: {region}");

//            //        CSDBTenantContext _dbTenant = new CSDBTenantContext();
//            //        CSDBContext _db = new CSDBContext();

//            //        var tenants = _dbTenant.AzTenants.Where(q => q.TenantId == vmProv.TenantID).Select(w => new TenantDetails
//            //        {
//            //            TenantId = w.TenantId,
//            //            TenantKey = w.TenantKey,
//            //            SubscriptionKey = w.SubscriptionKey,
//            //            GuacConnection = w.GuacConnection,
//            //            GuacamoleURL = w.GuacamoleURL,
//            //            EnvironmentCode = w.EnvironmentCode
//            //        }).FirstOrDefault();

//            //        var userGroups = _db.CloudLabsGroups.Where(w => w.TenantId == vmProv.TenantID).FirstOrDefault();

//            //        var _azureProd = Az(log);
//            //        var vm = _azureProd.Networks.GetByResourceGroup(vmProv.ResourceGroup, vmProv.ResourceGroup + "-vnet");
//            //        //var vm = _azureProd.Networks.GetByResourceGroup("CS-R2-BH081621-P-RGRP", "CS-R2-BH081621-P-VNET");

//            //        var subnet = string.Empty;
//            //        var lastSub = vm.Subnets.LastOrDefault();
//            //        //subnet = lastSub.Value.Name;

//            //        subnet = "default";
//            //        //if (lastSub.Value.NetworkInterfaceIPConfigurationCount == 240)
//            //        //{
//            //        //    var cidr = lastSub.Value.AddressPrefix.Split('.');
//            //        //    if (Convert.ToInt32(cidr[2]) == 240)
//            //        //    {
//            //        //        cidr[3] = (Convert.ToInt32(cidr[3]) + 1).ToString();
//            //        //    }
//            //        //    else
//            //        //    {
//            //        //        cidr[2] = (Convert.ToInt32(cidr[2]) + 1).ToString();
//            //        //    }
//            //        //    var addPrefix = string.Join(".", cidr);

//            //        //    var name = GenerateRandomSubnetName();

//            //        //    vm.Update().WithAddressSpace(addPrefix).Apply();
//            //        //    vm.Update().DefineSubnet(name).WithAddressPrefix(addPrefix).Attach().Apply();
//            //        //}
//            //        //else
//            //        //{
//            //        //    subnet = lastSub.Value.Name;
//            //        //}

//            //        log.LogInformation("Subnet done");

//            //        var publicIpAddress = _azureProd.PublicIPAddresses.Define($"IP-{vmProv.MachineName}")
//            //             .WithRegion(region)
//            //             .WithExistingResourceGroup(vmProv.ResourceGroup).WithDynamicIP()
//            //             .WithLeafDomainLabel(vmProv.MachineName.ToLower())
//            //             .Create();

//            //        log.LogInformation("PublicIp done");
//            //        var ipAdd = publicIpAddress.IPAddress;

//            //        var networkInterface = _azureProd.NetworkInterfaces.Define($"{vmProv.MachineName}")
//            //            .WithRegion(region)
//            //            .WithExistingResourceGroup(vmProv.ResourceGroup)
//            //            .WithExistingPrimaryNetwork(vm)
//            //            .WithSubnet(subnet)
//            //            .WithPrimaryPrivateIPAddressDynamic()
//            //            .WithExistingPrimaryPublicIPAddress(publicIpAddress)
//            //            .Create();

//            //        var DNS = networkInterface.InternalDnsNameLabel;

//            //        log.LogInformation("NetworkInterface Done");

//            //        if (vmProv.VETypeID == 1 || vmProv.VETypeID == 3)
//            //        {
//            //            IVirtualMachine createVm = _azureProd.VirtualMachines.Define(vmProv.MachineName)
//            //                .WithRegion(region)
//            //                .WithExistingResourceGroup(vmProv.ResourceGroup)
//            //                .WithExistingPrimaryNetworkInterface(networkInterface)
//            //                .WithStoredWindowsImage(vmProv.ImageName)
//            //                .WithAdminUsername(vmProv.Username)
//            //                .WithAdminPassword(vmProv.Password)
//            //                .WithComputerName(vmProv.MachineName)
//            //                .WithSize(Size(vmProv.Size))
//            //                .Create();
//            //        }

//            //        if (vmProv.VETypeID == 2 || vmProv.VETypeID == 4)
//            //        {
//            //            IVirtualMachine createVm = _azureProd.VirtualMachines.Define(vmProv.MachineName)
//            //                .WithRegion(region)
//            //                .WithExistingResourceGroup(vmProv.ResourceGroup)
//            //                .WithExistingPrimaryNetworkInterface(networkInterface)
//            //                .WithStoredLinuxImage(vmProv.ImageName)
//            //                .WithRootUsername(vmProv.Username)
//            //                .WithRootPassword(vmProv.Password)
//            //                .WithComputerName(vmProv.MachineName)
//            //                .WithSize(Size(vmProv.Size))
//            //                .Create();
//            //        }

//            //        log.LogInformation($"Virtualmachine Created");

//            //        InsertCustomerVMDetails(vmProv, operationId);

//            //        while (isTrue)
//            //        {
//            //            IVirtualMachine vmData = _azureProd.VirtualMachines.List().Where(vm => vm.Name.ToLower() == vmProv.MachineName.ToLower()).FirstOrDefault();

//            //            if (vmData.ProvisioningState.ToLower() == "failed")
//            //            {
//            //                UpdateMachineLab(vmProv.ResourceId, 3, 0, operationId);
//            //                isFailed = !isFailed;
//            //                isTrue = !isTrue;
//            //                log.LogInformation($"VM Failed");
//            //            }
//            //            else if (vmData.ProvisioningState.ToLower() == "succeeded")
//            //            {
//            //                log.LogInformation($"Virtual Machine was success created");
//            //                isTrue = !isTrue;
//            //            }
//            //            else
//            //                isTrue = true;
//            //        }

//            //        if (!isFailed)
//            //        {
//            //            if (vmProv.VETypeID == 1 || vmProv.VETypeID == 3) // 1 = windows, 3 = windows custom
//            //            {
//            //                ShutDownVMWithEnvVar(vmProv, log).Wait();

//            //                //LabsProvisionModel p = new LabsProvisionModel();
//            //                //p.apiprefix = userGroups.ApiPrefix;
//            //                //p.computerName = vmProv.MachineName;
//            //                //p.Fqdn = userGroups.CLUrl.Split("https://")[1].ToLower();
//            //                //p.location = GetEnvironmentVariable("Region");
//            //                //p.TenantId = GetEnvironmentVariable("ClientId");
//            //                //p.VirtualMachineName = vmProv.MachineName;
//            //                //p.ResourceGroupName = vmProv.ResourceGroup;

//            //                //log.LogInformation($"LabsProvisionModel ApiPrefix: {p.apiprefix}");
//            //                //log.LogInformation($"LabsProvisionModel computerName: {p.computerName}");
//            //                //log.LogInformation($"LabsProvisionModel Fqdn: {p.Fqdn}");
//            //                //log.LogInformation($"LabsProvisionModel location: {p.location}");
//            //                //log.LogInformation($"LabsProvisionModel TenantId: {p.TenantId}");
//            //                //log.LogInformation($"LabsProvisionModel VirtualMachineName: {p.VirtualMachineName}");

//            //                // InsertEnvironmentVar(p, log, _azureProd, vmProv, tenants, operationId);

//            //                //if (output.ToLower() != "bad")
//            //                //{
//            //                log.LogInformation("Status is deallocating!");

//            //                IVirtualMachine vmData1 = _azureProd.VirtualMachines.List().Where(vm => vm.Name.ToLower() == vmProv.MachineName.ToLower()).FirstOrDefault();
//            //                vmData1.Deallocate();

//            //                while (isLoop)
//            //                {
//            //                    Thread.Sleep(120000);
//            //                    //Task.Delay(180000);

//            //                    IVirtualMachine vmData2 = _azureProd.VirtualMachines.List().Where(vm => vm.Name.ToLower() == vmProv.MachineName.ToLower()).FirstOrDefault();

//            //                    vmData2.Deallocate();

//            //                    if (vmData2.PowerState.Value.Split("/")[1].ToLower() == "deallocated")
//            //                    {
//            //                        log.LogInformation("Status is deallocated!");

//            //                        var guacURL = AddMachineToDatabase(vmProv.MachineName, tenants, vmProv.Username, vmProv.Password, vmProv.VETypeID, vmProv.FQDN + "." + GetEnvironmentVariable("Region") + ".cloudapp.azure.com", log);

//            //                        UpdateMachineLabWithGuac(vmProv, guacURL, operationId, GetEnvironmentVariable("Region"), log);
//            //                        isLoop = !isLoop;
//            //                    }
//            //                    //}
//            //                }

//            //            }
//            //            else if (vmProv.VETypeID == 2 || vmProv.VETypeID == 4)    // 2 = linux, 4 = linux custom 
//            //            {
//            //                var guacURL = AddMachineToDatabase(vmProv.MachineName, tenants, vmProv.Username, vmProv.Password, vmProv.VETypeID, vmProv.FQDN + "." + GetEnvironmentVariable("Region") + ".cloudapp.azure.com", log);

//            //                UpdateMachineLabWithGuac(vmProv, guacURL, operationId, GetEnvironmentVariable("Region"), log);

//            //                IVirtualMachine vmData = _azureProd.VirtualMachines.List().Where(vm => vm.Name.ToLower() == vmProv.MachineName.ToLower()).FirstOrDefault();

//            //                vmData.DeallocateAsync();

//            //                log.LogInformation("Status is deallocated! Linux");
//            //            }
//            //        }
//            //    });

//            //    ////call AUTOMATION ACCOUNT


//            //    return new OkObjectResult("");

//            //}
//            //catch (Exception ex)
//            //{
//            //    log.LogInformation($"Errrroooorrr {ex.Message}");
//            //    return new OkObjectResult(ex.Message);

//            //}

//        }

//    }
//}
