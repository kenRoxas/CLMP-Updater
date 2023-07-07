//using System;
//using System.Collections.Generic;
//using System.Globalization;
//using System.IO;
//using System.Linq;
//using Microsoft.AspNetCore.Http;
//using Microsoft.Azure.WebJobs;
//using Microsoft.Azure.WebJobs.Extensions.Http;
//using Microsoft.Azure.WebJobs.Host;
//using Microsoft.Extensions.Logging;
//using OfficeOpenXml;
//using VMWAProvision.Models;
//using static VMWAProvision.Helpers.Helper;
//using static VMWAProvision.Helpers.AzureAz;
//using Microsoft.Azure.Management.Compute.Fluent;
//using VMWAProvision.Controller;
//using System.Threading.Tasks;

//namespace VMWAProvision
//{
//    public class VMNotification
//    {
//        [FunctionName("VMNotification")]
//        //public void Run([TimerTrigger("%CRON_TIME_Notification%")] TimerInfo myTimer, ILogger log)
//        public void Run5([HttpTrigger(AuthorizationLevel.Function, "get", "post", Route = null)] HttpRequest req, ILogger log)
//        {
//            try
//            {
//                CSDBContext _db = new CSDBContext();
//                CSDBTenantContext _dbTenant = new CSDBTenantContext();

//                List<VMListEmail> fiftyLists = new List<VMListEmail>();
//                List<VMListEmail> seventyFiveLists = new List<VMListEmail>();
//                List<VMListEmail> aDayLists = new List<VMListEmail>();
//                List<VMListEmail> expiredLists = new List<VMListEmail>();

//                MemoryStream ms;
//                ExcelPackage.LicenseContext = LicenseContext.NonCommercial;

//                double? fiftyValidity = 0;
//                double? seventyFiveValidity = 0;
//                double? aDayValidity = 0;
//                double? expiredValidity = 0;
//                string body = "";
//                int? validity = 0;

//                log.LogInformation($"C# Timer trigger function executed at: {DateTime.Now}");

//                var vm = _db.CloudLabUsers.Join(_db.MachineLabs, a => a.UserId, b => b.UserId, (a, b) => new { a, b })
//                    .Join(_db.CloudLabsSchedules, c => c.b.MachineLabsId, d => d.MachineLabsId, (c, d) => new { c, d })
//                    .Join(_db.VEProfiles, e => e.c.b.VEProfileId, f => f.VEProfileID, (e, f) => new { e, f })
//                    .Select(w => new NotificationVM
//                    {
//                        Dateprovision = w.e.c.b.DateProvision,
//                        CourseName = w.f.Name,
//                        Email = w.e.c.a.Email,
//                        ScheduledBy = w.e.c.b.ScheduledBy,
//                        InstructorRemaining = w.e.d.InstructorLabHours,
//                        LabHoursTotal = w.e.d.LabHoursTotal,
//                        ResourceId = w.e.c.b.ResourceId,
//                        TimeRemaining = w.e.d.TimeRemaining,
//                        VMName = w.e.c.b.VMName,
//                        UserGroupId = w.e.c.a.UserGroup,
//                        TenantId = w.e.c.a.TenantId
//                    }).OrderBy(x => x.UserGroupId).ToList();

//                foreach (var item in vm)
//                {
//                    VMListEmail vmLists = new VMListEmail();


//                    var isExtendAble = _db.BusinessGroups.Any(q => q.UserGroupId == item.UserGroupId);
//                    var BusinessId = _dbTenant.AzTenants.Where(q => q.TenantId == item.TenantId).FirstOrDefault().BusinessId;
//                    string userGroupName = _db.CloudLabsGroups.Where(q => q.CloudLabsGroupID == item.UserGroupId).FirstOrDefault().GroupName;

//                    vmLists.CourseName = item.CourseName;
//                    vmLists.Dateprovision = item.Dateprovision;
//                    vmLists.Email = item.Email;
//                    vmLists.GroupName = userGroupName;
//                    vmLists.InstructorRemaining = Math.Round((double)item.InstructorRemaining / 60);
//                    vmLists.LabHoursTotal = item.LabHoursTotal * 60;
//                    vmLists.ResourceId = item.ResourceId;
//                    vmLists.ScheduledBy = item.ScheduledBy;
//                    vmLists.VMName = item.VMName;
//                    vmLists.TimeRemaining = Math.Round((double)item.TimeRemaining / 60);

//                    //if client has a modified validity
//                    if (isExtendAble)
//                    {
//                        if (_db.BusinessGroups.Where(q => q.UserGroupId == item.UserGroupId).FirstOrDefault().ModifiedValidity != null)
//                            validity = _db.BusinessGroups.Where(q => q.UserGroupId == item.UserGroupId).FirstOrDefault().ModifiedValidity;
//                        else
//                            validity = _db.BusinessTypes.Where(q => q.BusinessId == BusinessId).FirstOrDefault().Validity;
//                    }
//                    else
//                        validity = _db.BusinessTypes.Where(q => q.BusinessId == BusinessId).FirstOrDefault().Validity;


//                    fiftyValidity = Math.Round((double)validity * .50); //50%
//                    seventyFiveValidity = Math.Round((double)validity * .75); //75%
//                    aDayValidity = validity - 1;
//                    expiredValidity = validity;

//                    if (DateTime.UtcNow.Date.Subtract(Convert.ToDateTime(item.Dateprovision).Date).TotalDays == fiftyValidity)
//                        fiftyLists.Add(vmLists);
//                    else if (DateTime.UtcNow.Date.Subtract(Convert.ToDateTime(item.Dateprovision).Date).TotalDays == seventyFiveValidity)
//                        seventyFiveLists.Add(vmLists);
//                    else if (DateTime.UtcNow.Date.Subtract(Convert.ToDateTime(item.Dateprovision).Date).TotalDays == aDayValidity)
//                        aDayLists.Add(vmLists);
//                    else if (DateTime.UtcNow.Date.Subtract(Convert.ToDateTime(item.Dateprovision).Date).TotalDays >= expiredValidity)
//                        expiredLists.Add(vmLists);
//                }

//                using (ExcelPackage package = new ExcelPackage())
//                {
//                    ExcelWorksheet worksheet = package.Workbook.Worksheets.Add("Expired VM");

//                    var headerRow = new List<string[]>()
//                    {
//                        new string[] { "ResourceId", "Email", "Course Name", "VM Name", "Time Remaining (Minutes)", "Lab Hours Total (Minutes)", "Instructor Remaining (Minutes)", "Date Provisioned", "Group Name", "Scheduled By" }
//                    };

//                    string headerRange = "A1:" + Char.ConvertFromUtf32(headerRow[0].Length + 64) + "1234";
//                    worksheet.Cells[headerRange].LoadFromArrays(headerRow);

//                    worksheet.Cells.AutoFitColumns();

//                    if (fiftyLists.Count > 0)
//                    {
//                        body = @$"<html><head></head><body><p style='font-weight: bold; text-align: center; margin-bottom: 5em;'>*** THIS IS A SYSTEM GENERATED E-MAIL.  PLEASE DO NOT REPLY TO THIS MESSAGE. ***</p><p>The attached file contains all virtual machines scheduled to expire in {fiftyValidity} days from now.</p>" +
//                            "<p>In order to prevent unnecessary deletion of the VMs, you can request an extension as early as now.</p> </body></html>";

//                        worksheet.Cells[2, 1].LoadFromCollection(fiftyLists);
//                        ms = new MemoryStream(package.GetAsByteArray());
//                        SendMail(ms, @$"Notice for VMs that have {fiftyValidity} day left", body);
//                    }
//                    if (seventyFiveLists.Count > 0)
//                    {
//                        body = @$"<html><head></head><body><p style='font-weight: bold; text-align: center; margin-bottom: 5em;'>*** THIS IS A SYSTEM GENERATED E-MAIL.  PLEASE DO NOT REPLY TO THIS MESSAGE. ***</p><p>The attached file contains all virtual machines scheduled to expire in {validity - seventyFiveValidity} days from now.</p>" +
//                            "<p>In order to prevent unnecessary deletion of the VMs, you can request an extension as early as now.</p> </body></html>";

//                        string filename = DateTime.UtcNow.ToShortDateString();
//                        worksheet.Cells[2, 1].LoadFromCollection(seventyFiveLists);
//                        ms = new MemoryStream(package.GetAsByteArray());
//                        SendMail(ms, @$"Notice for VMs that have {validity - seventyFiveValidity} day left", body);
//                    }
//                    if (aDayLists.Count > 0)
//                    {
//                        body = @$"<html><head></head><body><p style='font-weight: bold; text-align: center; margin-bottom: 5em;'>*** THIS IS A SYSTEM GENERATED E-MAIL.  PLEASE DO NOT REPLY TO THIS MESSAGE. ***</p><p>The attached file contains all virtual machines scheduled to expire tomorrow.</p>" +
//                            "<p>In order to prevent unnecessary deletion of the VMs, you can request an extension as early as now.</p> </body></html>";

//                        string filename = DateTime.UtcNow.ToShortDateString();
//                        worksheet.Cells[2, 1].LoadFromCollection(aDayLists);
//                        ms = new MemoryStream(package.GetAsByteArray());
//                        SendMail(ms, @$"Notice for VMs that have 1 day left", body);
//                    }
//                    if (expiredLists.Count > 0)
//                    {
//                        body = @$"<html><head></head><body><p style='font-weight: bold; text-align: center; margin-bottom: 5em;'>*** THIS IS A SYSTEM GENERATED E-MAIL.  PLEASE DO NOT REPLY TO THIS MESSAGE. ***</p><p>The attached file contains all expired virtual machines.</p>" +
//                            "<p>It is important to note that all expired VMs will be automatically deleted at 6:00PM Metro Manila time (GMT+8) if no request is received to extend them. </p> </body></html>";

//                        string filename = DateTime.UtcNow.ToShortDateString();
//                        worksheet.Cells[2, 1].LoadFromCollection(expiredLists);
//                        ms = new MemoryStream(package.GetAsByteArray());
//                        SendMail(ms, @$"Expired VMs", body);
//                    }
//                }

//            }
//            catch (Exception e)
//            {
//                log.LogInformation(e.Message);
//            }


//        }


//        [FunctionName("CreateSched")]
//        //public void Run([TimerTrigger("0 */5 * * * *")] TimerInfo myTimer, ILogger log)
//        public void Run([HttpTrigger(AuthorizationLevel.Function, "get", "post", Route = null)] HttpRequest req, ILogger log)
//        {
//            try
//            {
//                CSDBContext _db = new CSDBContext();
//                CSDBTenantContext _dbTenant = new CSDBTenantContext();

//                var veprofiles = _db.VEProfiles.ToList();
//                var users = _db.CloudLabUsers.Select(q => q.UserId).ToList();


//                const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
//                var random = new Random();

//                foreach (var item in veprofiles)
//                {
//                    foreach (var item1 in users)
//                    {
//                        if (!_db.MachineLabs.Any(q => q.UserId == item1 && q.VEProfileId == item.VEProfileID))
//                        {
//                            var _ml = new MachineLabs();
//                            var _mlogs = new MachineLogs();
//                            var _cs = new CloudLabsSchedules();
//                            var cg = new CourseGrants();
//                            var vmname = new string(Enumerable.Repeat(chars, 6)
//                       .Select(s => s[random.Next(s.Length)])
//                       .ToArray());

//                            _ml.DateProvision = DateTime.Today.ToString();
//                            _ml.MachineName = vmname;
//                            _ml.ResourceId = "ResourceId" + vmname;
//                            _ml.UserId = item1;
//                            _ml.VEProfileId = item.VEProfileID;
//                            _ml.IsStarted = 1; // provisioning 
//                            _ml.IsDeleted = 0;
//                            _ml.MachineStatus = "Provisioning";
//                            _ml.ScheduledBy = "sabaw";
//                            _ml.VMName = vmname;
//                            _ml.Username = "username";
//                            _ml.Password = "password";
//                            _ml.GuacDNS = "sample";
//                            _ml.FQDN = "sample";
//                            _db.MachineLabs.Add(_ml);
//                            _db.SaveChanges();

//                            _mlogs.LastStatus = "STOPPED";
//                            _mlogs.Logs = "STOPPED";
//                            _mlogs.ModifiedDate = DateTime.Today;
//                            _mlogs.ResourceId = _ml.ResourceId;
//                            _mlogs.RequestId = null;
//                            _db.MachineLogs.Add(_mlogs);
//                            _db.SaveChanges();

//                            _cs.VEProfileID = item.VEProfileID;
//                            _cs.UserId = item1;
//                            _cs.TimeRemaining = 3600;
//                            _cs.LabHoursTotal = 3600;
//                            _cs.StartLabTriggerTime = null;
//                            _cs.RenderPageTriggerTime = null;
//                            _cs.InstructorLabHours = 7200;
//                            _cs.InstructorLastAccess = null;
//                            _cs.MachineLabsId = _ml.MachineLabsId;
//                            _db.CloudLabsSchedules.Add(_cs);
//                            _db.SaveChanges();

//                            cg.UserID = item1;
//                            cg.VEProfileID = item.VEProfileID;
//                            cg.IsCourseGranted = true;
//                            cg.VEType = 1;
//                            cg.GrantedBy = 1;
//                            _db.CourseGrants.Add(cg);
//                            _db.SaveChanges();
//                        }

//                    }
//                }

//            }
//            catch (Exception e)
//            {
//                log.LogInformation(e.Message);
//            }


//        }

//        [FunctionName("VMExpiredAutomatedDeletion")]
//        //public void Run([TimerTrigger("0 */5 * * * *")] TimerInfo myTimer, ILogger log)
//        public async Task Run7([HttpTrigger(AuthorizationLevel.Function, "get", "post", Route = null)] HttpRequest req, ILogger log)
//        {
//            try
//            {
//                CSDBContext _db = new CSDBContext();
//                CSDBTenantContext _dbTenant = new CSDBTenantContext();

//                List<VMDeleteList> expiredLists = new List<VMDeleteList>();

//                ExcelPackage.LicenseContext = LicenseContext.NonCommercial;

//                double? expiredValidity = 0;
//                int? validity = 0;

//                log.LogInformation($"C# Timer trigger function executed at: {DateTime.Now}");

//                var _azureProd = Az(log);

//                var vm = _db.CloudLabUsers.Join(_db.MachineLabs, a => a.UserId, b => b.UserId, (a, b) => new { a, b })
//                    .Join(_db.CloudLabsSchedules, c => c.b.MachineLabsId, d => d.MachineLabsId, (c, d) => new { c, d })
//                    .Join(_db.VEProfiles, e => e.c.b.VEProfileId, f => f.VEProfileID, (e, f) => new { e, f })
//                    .Select(w => new NotificationVM
//                    {
//                        Dateprovision = w.e.c.b.DateProvision,
//                        CourseName = w.f.Name,
//                        Email = w.e.c.a.Email,
//                        ScheduledBy = w.e.c.b.ScheduledBy,
//                        InstructorRemaining = w.e.d.InstructorLabHours,
//                        LabHoursTotal = w.e.d.LabHoursTotal,
//                        ResourceId = w.e.c.b.ResourceId,
//                        TimeRemaining = w.e.d.TimeRemaining,
//                        VMName = w.e.c.b.VMName,
//                        UserGroupId = w.e.c.a.UserGroup,
//                        TenantId = w.e.c.a.TenantId
//                    }).OrderBy(x => x.UserGroupId).ToList();

//                foreach (var item in vm)
//                {
//                    VMDeleteList vmLists = new VMDeleteList();

//                    var environment = "";
//                    var resourceGroup = "";
//                    var isExtendAble = _db.BusinessGroups.Any(q => q.UserGroupId == item.UserGroupId);
//                    var BusinessId = _dbTenant.AzTenants.Where(q => q.TenantId == item.TenantId).FirstOrDefault().BusinessId;
//                    var userGroup = _db.CloudLabsGroups.Where(q => q.CloudLabsGroupID == item.UserGroupId).FirstOrDefault();
//                    var tenant = _dbTenant.AzTenants.Where(q => q.TenantId == item.TenantId).FirstOrDefault();
//                    var veprofileId = _db.VEProfiles.Join(_db.MachineLabs,
//                       a => a.VEProfileID,
//                       b => b.VEProfileId,
//                       (a, b) => new { a, b }).Where(q => q.b.ResourceId == item.ResourceId).FirstOrDefault().a.VEProfileID;

//                    var VMvhdUrl = _db.VEProfiles.Where(q => q.VEProfileID == veprofileId).Select(w => new { w.VirtualEnvironmentID, w.VEProfileID }).Join(_db.VirtualEnvironmentImages,
//                    a => a.VirtualEnvironmentID,
//                    b => b.VirtualEnvironmentID,
//                    (a, b) => new { a, b }).FirstOrDefault().b.Name;

//                    var storageAccountName = VMvhdUrl.Substring(VMvhdUrl.IndexOf("https://") + 8, VMvhdUrl.IndexOf(".") - VMvhdUrl.IndexOf("https://") - 8);

//                    if (userGroup.CLPrefix.Length == 3)
//                    {
//                        environment = tenant.EnvironmentCode.Trim() == "D" ? "DEV" : tenant.EnvironmentCode.Trim() == "Q" ? "QA" : tenant.EnvironmentCode.Trim() == "U" ? "DMO" : "PRD";
//                        resourceGroup = "CS-" + environment + "-" + userGroup.CLPrefix;
//                    }
//                    else
//                    {
//                        environment = tenant.EnvironmentCode.Trim();
//                        resourceGroup = "";
//                        //resourceGroup = "CS-" + userGroup.CLPrefix + "-" + environment + "-RGRP";

//                    }

//                    vmLists.VMName = item.VMName;
//                    vmLists.ClientCode = tenant.ClientCode;
//                    vmLists.ResourceGroup = resourceGroup;
//                    vmLists.StorageAccountName = storageAccountName;

//                    //if client has a modified validity
//                    if (isExtendAble)
//                    {
//                        if (_db.BusinessGroups.Where(q => q.UserGroupId == item.UserGroupId).FirstOrDefault().ModifiedValidity != null)
//                            validity = _db.BusinessGroups.Where(q => q.UserGroupId == item.UserGroupId).FirstOrDefault().ModifiedValidity;
//                        else
//                            validity = _db.BusinessTypes.Where(q => q.BusinessId == BusinessId).FirstOrDefault().Validity;
//                    }
//                    else
//                        validity = _db.BusinessTypes.Where(q => q.BusinessId == BusinessId).FirstOrDefault().Validity;

//                    expiredValidity = validity;

//                    if (DateTime.UtcNow.Date.Subtract(Convert.ToDateTime(item.Dateprovision).Date).TotalDays >= 1)
//                        expiredLists.Add(vmLists);
//                }

//                foreach (var item in expiredLists)
//                {
//                    await Task.Run(() =>
//                    {
//                        VMOperation delete = new VMOperation();
//                        IVirtualMachine vmData = _azureProd.VirtualMachines.List().Where(vm => vm.Name.ToLower() == item.VMName.ToLower()).FirstOrDefault();
//                        string vhdName = vmData.OSUnmanagedDiskVhdUri.Split("/")[vmData.OSUnmanagedDiskVhdUri.Split("/").Length - 1];
                        
//                        if (vmData != null)
//                        {
//                            if (item.ResourceGroup == "")
//                                item.ResourceGroup = vmData.ResourceGroupName;

//                            if (_azureProd.VirtualMachines.GetByResourceGroup(item.ResourceGroup, item.VMName) != null)
//                                _azureProd.VirtualMachines.DeleteByResourceGroup(item.ResourceGroup, item.VMName);
//                            if (_azureProd.PublicIPAddresses.GetByResourceGroup(item.ResourceGroup, item.VMName) != null)
//                                _azureProd.PublicIPAddresses.DeleteByResourceGroup(item.ResourceGroup, item.VMName);
//                            if (_azureProd.NetworkInterfaces.GetByResourceGroup(item.ResourceGroup, item.VMName) != null)
//                                _azureProd.NetworkInterfaces.DeleteByResourceGroup(item.ResourceGroup, item.VMName);

//                            var isDeleted = delete.DeleteVhd(_azureProd, item.StorageAccountName, item.VMName, item.ResourceGroup, vhdName, log);

//                        }
//                    });
                   
//                }

//                ////////////////////////
//                /*
//                 * loop expiredList
//                 * delete vm
//                 * delete ip
//                 * delete nic
//                 * delete vhd
//                 * Azure.Storage.Blobs
//                 */

//            }
//            catch (Exception e)
//            {
//                log.LogInformation(e.Message);
//            }

//        }

//    }
//}
