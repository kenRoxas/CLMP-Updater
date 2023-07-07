using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using VMWAProvision.Models;
using Newtonsoft.Json.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Linq;
using System.Collections.Generic;
using System.Text;
using static VMWAProvision.Helpers.Helper;
using VMWAProvision.Model;

namespace VMWAProvision
{
    public static class AWSProvision
    {
        public static string AWSVM = GetEnvironmentVariable("AWSVM");

        [FunctionName("AWSProvision")]
        public static async Task<IActionResult> Run([HttpTrigger(AuthorizationLevel.Function, "get", "post", Route = null)] HttpRequestMessage req, ILogger log)
        {
            CSDBContext _db = new CSDBContext();
            CSDBTenantContext _dbTenant = new CSDBTenantContext();

            log.LogInformation("C# HTTP trigger function processed a request.");

            try
            {
                dynamic body = await req.Content.ReadAsStringAsync();
                var AWSdata = JsonConvert.DeserializeObject<AWSData>(body as string);
                
                HttpResponseMessage responseAWS = null;
                HttpResponseMessage responseGetDetails = null;
                JObject getDetails = null;
                ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;
                HttpClient clientAWS = new HttpClient();

                clientAWS.BaseAddress = new Uri(AWSVM);
                clientAWS.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

                var groupId = _db.CloudLabUsers.Where(q => q.UserId == AWSdata.UserId).FirstOrDefault().UserGroup;
                var imageId = _db.VEProfiles.Join(_db.VirtualEnvironmentImages, a => a.VirtualEnvironmentID, b => b.VirtualEnvironmentID, (a, b) => new { a, b }).Where(q => q.a.VEProfileID == AWSdata.VEProfileId && q.b.GroupId == groupId).FirstOrDefault().b.Name;

                var tenant = _dbTenant.AzTenants.Where(q => q.TenantId == AWSdata.TenantId).Select(w => new { w.GuacConnection, w.GuacamoleURL, w.EnvironmentCode, w.ClientCode }).FirstOrDefault();
                var environment = tenant.EnvironmentCode.Replace(" ", String.Empty) == "D" ? "Staging" : tenant.EnvironmentCode.Replace(" ", String.Empty) == "Q" ? "QA" : tenant.EnvironmentCode == "U" ? "Demo" : "Prod";
                var envi = tenant.EnvironmentCode.Replace(" ", String.Empty) == "D" ? "DEV" : tenant.EnvironmentCode == "Q" ? "QA" : tenant.EnvironmentCode == "U" ? "DMO" : "PRD";

                var hours = _db.VEProfileLabCreditMappings.Where(q => q.VEProfileID == AWSdata.VEProfileId && q.GroupID == groupId).Select(w => new { w.CourseHours, w.TotalCourseHours }).FirstOrDefault();

                const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
                var random = new Random();

                var instanceName = new string(
                       Enumerable.Repeat(chars, 6)
                           .Select(s => s[random.Next(s.Length)])
                           .ToArray());
                log.LogInformation("Instance Name:" + instanceName);

                string[] sg = { "sg-0663e3fcbc92db0af" };

                var TagSpec = new List<TagSpecifications>();
                var tagSpec = new TagSpecifications();

                tagSpec.ResourceType = "instance";

                tagSpec.Tags = new List<Tags>
            {
                new Tags{Key = "Name", Value = envi + '-' + tenant.ClientCode +'-' + instanceName},
                new Tags{Key = "RESOURCE_GROUP", Value = envi + '-' + tenant.ClientCode + '-'},
                new Tags{Key = "STUDENT_ID", Value = "UI" + AWSdata.UserId.ToString() + "VE" + AWSdata.VEProfileId},
            };

                TagSpec.Add(tagSpec);

                var ec2 = new EC2
                {
                    InstanceType = AWSdata.MachineSize,
                    MaxCount = 1,
                    MinCount = 1,
                    ImageId = imageId,
                    KeyName = "cloudswyft-windows-instances",
                    SecurityGroupIds = sg,
                    TagSpecifications = TagSpec
                };

                var message = new AWSJson
                {
                    account_id = "827347782581",
                    ec2_details = ec2,
                    region = "ap-southeast-1",
                    root = "true"
                };


                var data = JsonConvert.SerializeObject(message);

                responseAWS = await clientAWS.PostAsync("dev/provision_vm", new StringContent(data, Encoding.UTF8, "application/json"));

                log.LogInformation("Result:" + responseAWS.Content.ReadAsStringAsync().Result);
                var details = JObject.Parse(responseAWS.Content.ReadAsStringAsync().Result);

                var InstanceId = details.SelectToken("Instances[0].InstanceId").ToString();

                CloudLabsSchedules cls = new CloudLabsSchedules();
                MachineLabs ml = _db.MachineLabs.Where(q => q.UserId == AWSdata.UserId && q.VEProfileId == AWSdata.VEProfileId).FirstOrDefault();
                CourseGrants cg = new CourseGrants();
                MachineLogs mlog = new MachineLogs();

                ml.ResourceId = InstanceId;
                ml.VMName = envi + '-' + tenant.ClientCode + '-' + instanceName;


                mlog.ResourceId = InstanceId;
                mlog.LastStatus = "Provisioning";
                mlog.Logs = '(' + mlog.LastStatus + ')' + DateTime.UtcNow;
                mlog.ModifiedDate = DateTime.UtcNow;

                _db.MachineLogs.Add(mlog);
                _db.SaveChanges();

                cls.VEProfileID = AWSdata.VEProfileId;
                cls.UserId = AWSdata.UserId;
                cls.TimeRemaining = TimeSpan.FromHours(hours.CourseHours).TotalSeconds;
                cls.LabHoursTotal = hours.CourseHours;
                cls.MachineLabsId = ml.MachineLabsId;
                cls.InstructorLabHours = TimeSpan.FromHours(2).TotalSeconds;

                _db.CloudLabsSchedules.Add(cls);
                _db.SaveChanges();

                var jsonDetails = new
                {
                    instance_id = InstanceId,
                    region = "ap-southeast-1"
                };
                var jsonData = JsonConvert.SerializeObject(jsonDetails);

                responseGetDetails = await clientAWS.PostAsync("dev/get_vm_details", new StringContent(jsonData, Encoding.UTF8, "application/json"));
                getDetails = JObject.Parse(responseGetDetails.Content.ReadAsStringAsync().Result);

                var isRunning = getDetails.SelectToken("Reservations[0].Instances[0].State.Name").ToString();
                return new OkObjectResult("");

            }
            catch (Exception e)
            {
                log.LogInformation($"Error AWSProvision: {e.Message}");
                return new BadRequestResult();

            }
        }
    }
}
