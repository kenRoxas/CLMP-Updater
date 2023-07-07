//using Microsoft.Azure.Management.Fluent;
//using Microsoft.Azure.Management.ResourceManager.Fluent;
//using Microsoft.Azure.Management.ResourceManager.Fluent.Authentication;
//using Microsoft.Azure.Management.ResourceManager.Fluent.Core;
//using Microsoft.Extensions.Logging;
//using Newtonsoft.Json;
//using Newtonsoft.Json.Linq;
//using System;
//using System.IO;
//using System.Collections.Generic;
//using System.Text;
//using VMWAProvision.Models;
//using static VMWAProvision.Helpers.Helper;
//using Microsoft.Azure.Management.Compute.Fluent;
//using System.Linq;
//using System.Threading;

//namespace VMWAProvision
//{
//    public static class EnvironmentVariableVersion2
//    {
//        public static string InsertEnvironmentVar(LabsProvisionModel labsProvision, ILogger log, IAzure _azureProd, ProvisionDetailsVM vmProv, TenantDetails tenants, string OperationId)
//        {
//            //if (string.IsNullOrEmpty(labsProvision.TenantId) ||
//            //    string.IsNullOrEmpty(labsProvision.VirtualMachineName) ||
//            //    string.IsNullOrEmpty(labsProvision.location) ||
//            //    string.IsNullOrEmpty(labsProvision.Fqdn) ||
//            //    string.IsNullOrEmpty(labsProvision.apiprefix) ||
//            //    string.IsNullOrEmpty(labsProvision.ResourceGroupName))
//            //{
//            //    log.LogInformation("Incorect Request Body.");

//            //    return "Bad";
//            //}

//            string tenantId = labsProvision.TenantId;
//            string virtualMachineName = labsProvision.VirtualMachineName.ToUpper();
//            string resourceGroupName = labsProvision.ResourceGroupName.ToUpper();
//            string Fqdn = labsProvision.Fqdn;
//            string apiprefix = labsProvision.apiprefix;
//            string location = labsProvision.location;
//            string computerName = labsProvision.computerName;

//            log.LogInformation($"tenantId: {tenantId}");
//            log.LogInformation($"virtualMachineName: {virtualMachineName}");
//            log.LogInformation($"resourceGroupName: {resourceGroupName}");
//            log.LogInformation($"Fqdn: {Fqdn}");
//            log.LogInformation($"apiprefix: {apiprefix}");
//            var isLoop = true;

//            string createEnvironmentVariablesPsUrl = GetEnvironmentVariable("CreateEnvironmentVariablesPsUrl");

//            try
//            {
//                JObject templateParameterObjectCustomExtension = GetJObject(Properties.Resources.windows_template_custom_extension);

//                templateParameterObjectCustomExtension.SelectToken("parameters.vmName")["defaultValue"] = virtualMachineName;
//                templateParameterObjectCustomExtension.SelectToken("parameters.location")["defaultValue"] = location;
//                templateParameterObjectCustomExtension.SelectToken("parameters.fileUris")["defaultValue"] = createEnvironmentVariablesPsUrl;
//                templateParameterObjectCustomExtension.SelectToken("parameters.arguments")["defaultValue"] = $"-ResourceGroupName {resourceGroupName} -VirtualMachineName {virtualMachineName} -ComputerName {computerName} -TenantId {tenantId} -GroupCode {apiprefix} -Fqdn {Fqdn}";

//                string uniqueId = Guid.NewGuid().ToString().Replace("-", "");
//                string deploymentName = $"virtual-machine-extension-{uniqueId}".ToLower();
//                log.LogInformation($"Deploying virtual-machine-extension-{uniqueId}");

//                log.LogInformation("Setting up system environment");

//                IDeployment vmExtensionDeployment = _azureProd.Deployments.Define(deploymentName)
//                    .WithExistingResourceGroup(resourceGroupName)
//                    .WithTemplate(templateParameterObjectCustomExtension)
//                    .WithParameters("{}")
//                    .WithMode(Microsoft.Azure.Management.ResourceManager.Fluent.Models.DeploymentMode.Incremental)
//                    .Create();

//                log.LogInformation("Setting up system environment is done");

//                log.LogInformation("Status is deallocating!");

//                IVirtualMachine vmData1 = _azureProd.VirtualMachines.List().Where(vm => vm.Name.ToLower() == labsProvision.VirtualMachineName.ToLower()).FirstOrDefault();
//                vmData1.Deallocate();

//                while (isLoop)
//                {
//                    Thread.Sleep(120000);
//                    //Task.Delay(180000);

//                    //IVirtualMachine vmData2 = _azureProd.VirtualMachines.List().Where(vm => vm.Name.ToLower() == vmProv.MachineName.ToLower()).FirstOrDefault();

//                    //vmData2.Deallocate();

//                    if (vmData1.PowerState.Value.Split("/")[1].ToLower() == "deallocated")
//                    {
//                        log.LogInformation("Status is deallocated!");

//                        var guacURL = AddMachineToDatabase(vmProv.MachineName, tenants, vmProv.Username, vmProv.Password, vmProv.VETypeID, vmProv.FQDN + "." + GetEnvironmentVariable("Region") + ".cloudapp.azure.com", log);

//                        UpdateMachineLabWithGuac(vmProv, guacURL, OperationId, GetEnvironmentVariable("Region"), log);
//                        isLoop = !isLoop;
//                    }
//                }

//                return $"virtual-machine-extension-{uniqueId} deployment is done";

//            }
//            catch (Exception e)
//            {

//                log.LogError(e.Message);

//                return "Bad";
//            }
//        }
//    }
//}
