
using Microsoft.Azure.Management.Compute.Fluent.Models;
using Microsoft.Extensions.Logging;
using MySql.Data.MySqlClient;
using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Mail;
using System.Security.Cryptography;
using System.Text;
using VMWAProvision.Models;
using System.Data.Entity;
using Microsoft.Azure.Management.ResourceManager.Fluent.Core;
using System.Net.Http;
using System.Net.Http.Headers;
using Newtonsoft.Json;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using System.Net.Mime;
using VMWAProvision.Model;

namespace VMWAProvision.Helpers
{
    public static class Helper
    {
        //public static CSDBContext _db = new CSDBContext();
        //public static CSDBCustomerVMContext _dbCustomer = new CSDBCustomerVMContext();        
        public static string ClientEnvironment = GetEnvironmentVariable("ClientEnvironment"); 
        public static string SendGridUsername = GetEnvironmentVariable("SendGridUsername"); 
        public static string SendGridPassword = GetEnvironmentVariable("SendGridPassword"); 
        public static string SendGridSender = GetEnvironmentVariable("SendGridSender"); 

        public static string GetEnvironmentVariable(string name)
        {
            return Environment.GetEnvironmentVariable(name, EnvironmentVariableTarget.Process);
        }

        public enum VMStatus : int
        {
            Deallocated = 0,
            Running = 1,
            Deallocating = 2,
            Failed = 3,
            Provisioning = 4,
            Starting = 5
        }
        public static string GenerateTenRandomName()
        {
            const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
            var random = new Random();
            var result = new string(
                Enumerable.Repeat(chars, 10)
                    .Select(s => s[random.Next(s.Length)])
                    .ToArray());

            return $"{result}";
        }
        public static string GenerateUserNameRandomName()
        {
            const string chars = "abcdefghijklmnopqrstuvwxyz0123456789";
            var random = new Random();
            var result = new string(
                Enumerable.Repeat(chars, 7)
                    .Select(s => s[random.Next(s.Length)])
                    .ToArray());

            return $"{result}";
        }
        public static string GeneratePasswordRandomName()
        {
            const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789abcdefghijklmnopqrstuvwxyz@/$!";
            var random = new Random();
            var result = new string(
                Enumerable.Repeat(chars, 13)
                    .Select(s => s[random.Next(s.Length)])
                    .ToArray());

            return $"{result}" +"3!2";
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
        public static void UpdateMachineLabWithGuac(ProvisionDetailsVM vmProv, string guacURL, string operationId, string region, ILogger log) {
            try
            {

                CSDBContext _db = new CSDBContext();
                CSDBCustomerVMContext _dbCustomer = new CSDBCustomerVMContext();
                log.LogInformation($"Enter UpdateMachineLabWithGuac");

                log.LogInformation($"vmProv {vmProv}");
                log.LogInformation($"guacURL {guacURL}");
                log.LogInformation($"operationId {operationId}");
                log.LogInformation($"region {region}");


                var _ml = _db.MachineLabs.Where(q => q.ResourceId == vmProv.ResourceId).FirstOrDefault();
                log.LogInformation($"_ml {_ml}");
                var _mLogs = _db.MachineLogs.Where(q => q.ResourceId == vmProv.ResourceId).FirstOrDefault();
                log.LogInformation($"_mLogs {_mLogs}");
                //var _vmCustomer = _dbCustomer.VirtualMachineDetails.Where(q => q.ResourceId == vmProv.ResourceId).FirstOrDefault();
                //log.LogInformation($"_vmCustomer {_vmCustomer}");
                var _status = _db.VMStatus.Where(q => q.Id == 0).FirstOrDefault().Status;
                log.LogInformation($"_status {_status}");
                var courseHours = _db.VEProfileLabCreditMappings.Where(q => q.VEProfileID == _ml.VEProfileId && q.GroupID == _db.CloudLabUsers.Where(w => w.UserId == _ml.UserId).FirstOrDefault().UserGroup).FirstOrDefault().CourseHours;
                log.LogInformation($"courseHours {courseHours}");

                CloudLabsSchedules _cs = new CloudLabsSchedules();

                _ml.IsStarted = 0;
                _ml.MachineStatus = _status;
                _ml.RunningBy = 0;
                _ml.GuacDNS = guacURL;
                _ml.MachineName = vmProv.MachineName;
                _ml.VMName = vmProv.MachineName;
                _ml.Username = vmProv.Username;
                _ml.Password = Encrypt(vmProv.Password);
                _ml.FQDN = vmProv.FQDN + "." + region + ".cloudapp.azure.com";
                _ml.DateProvision = DateTime.UtcNow;
                _db.Entry(_ml).State = EntityState.Modified;
                _db.SaveChanges();

                _mLogs.ModifiedDate = DateTime.UtcNow;
                _mLogs.LastStatus = _status;
                _mLogs.Logs = "(" + _status + ")" + DateTime.UtcNow + "---" + _mLogs.Logs;
                _db.Entry(_mLogs).State = EntityState.Modified;
                _db.SaveChanges();

                if(_dbCustomer.VirtualMachineDetails.Any(q => q.ResourceId == vmProv.ResourceId))
                {
                    var _vmCustomer = _dbCustomer.VirtualMachineDetails.Where(q => q.ResourceId == vmProv.ResourceId).FirstOrDefault();
                    _vmCustomer.DateLastModified = DateTime.UtcNow;
                    _vmCustomer.Status = 0;
                    _vmCustomer.OperationId = operationId;
                    _dbCustomer.Entry(_vmCustomer).State = EntityState.Modified;
                    _dbCustomer.SaveChanges();
                }
                else
                {
                    VirtualMachineDetails _vmCustomer = new VirtualMachineDetails();
                    _vmCustomer.ResourceId = vmProv.ResourceId;
                    _vmCustomer.DateLastModified = DateTime.UtcNow;
                    _vmCustomer.DateCreated = DateTime.UtcNow;
                    _vmCustomer.Status = 0;
                    _vmCustomer.VMName = vmProv.MachineName;
                    _vmCustomer.FQDN = vmProv.FQDN + "." + GetEnvironmentVariable("Region") + ".cloudapp.azure.com";
                    _vmCustomer.OperationId = operationId;

                    _dbCustomer.VirtualMachineDetails.Add(_vmCustomer);
                    _dbCustomer.SaveChanges();
                }

                _cs.VEProfileID = vmProv.VEProfileID;
                _cs.UserId = vmProv.UserID;
                _cs.TimeRemaining = courseHours * 60;
                _cs.LabHoursTotal = courseHours * 60;
                _cs.StartLabTriggerTime = null;
                _cs.RenderPageTriggerTime = null;
                _cs.InstructorLabHours = 7200;
                _cs.InstructorLastAccess = null;
                _cs.MachineLabsId = _ml.MachineLabsId;
                _db.CloudLabsSchedules.Add(_cs);
                _db.SaveChanges();
            }
            catch(Exception e)
            {
                log.LogInformation($"ERROR IN UpdateMachineLabWithGuac: {e.Message}");
            }
       
        }
        public static void UpdateMachineLab(string resourceId, int statusId, int runBy, string operationId, string machineStatus)
        {
            try
            {
                CSDBContext _db = new CSDBContext();
                CSDBCustomerVMContext _dbCustomer = new CSDBCustomerVMContext();

                var _ml = _db.MachineLabs.Where(q => q.ResourceId == resourceId).FirstOrDefault();
                var _mLogs = _db.MachineLogs.Where(q => q.ResourceId == resourceId).FirstOrDefault();
                var _status = _db.VMStatus.Where(q => q.Id == statusId).FirstOrDefault().Status;

                _ml.IsStarted = statusId;
                _ml.MachineStatus = machineStatus;
                _ml.RunningBy = runBy;
                _db.Entry(_ml).State = EntityState.Modified;
                _db.SaveChanges();

                _mLogs.ModifiedDate = DateTime.UtcNow;
                _mLogs.LastStatus = machineStatus;
                _mLogs.Logs = "(" + machineStatus + ")" + DateTime.UtcNow + "---" + _mLogs.Logs;
                _db.Entry(_mLogs).State = EntityState.Modified;
                _db.SaveChanges();
                
                if(_dbCustomer.VirtualMachineDetails.Any(q => q.ResourceId == resourceId))
                {
                    var _vmCustomer = _dbCustomer.VirtualMachineDetails.Where(q => q.ResourceId == resourceId).FirstOrDefault(); 
                    
                    _vmCustomer.DateLastModified = DateTime.UtcNow;
                    _vmCustomer.Status = statusId;
                    _vmCustomer.OperationId = operationId;
                    _dbCustomer.Entry(_vmCustomer).State = EntityState.Modified;
                    _dbCustomer.SaveChanges();
                }

                
            }
            catch(Exception e)
            {
                var s = e.Message;
            }
        }
        public static void InsertCustomerVMDetails(ProvisionDetailsVM vmData, string operationId)
        {
            CSDBContext _db = new CSDBContext();
            CSDBCustomerVMContext _dbCustomer = new CSDBCustomerVMContext();
            VirtualMachineDetails vmDetails = new VirtualMachineDetails();

           //using (var vmDetails = new VirtualMachineDetails())
           // {
                vmDetails.DateCreated = DateTime.UtcNow;
                vmDetails.DateLastModified = DateTime.UtcNow;
                vmDetails.FQDN = vmData.FQDN;
                vmDetails.ResourceId = vmData.ResourceId;
                vmDetails.Status = 5;
                vmDetails.VMName = vmData.MachineName;
                vmDetails.OperationId = operationId == null ? "Test" : operationId;

                _dbCustomer.VirtualMachineDetails.Add(vmDetails);
                _dbCustomer.SaveChanges();
                _dbCustomer.Dispose();
           // }

            
        }
        public static void UpdateMachineDatabase(MachineLabs ml, string guacURL, string region, ILogger log, int vmStatus)
        {
            try
            {

                CSDBContext _db = new CSDBContext();
                CSDBCustomerVMContext _dbCustomer = new CSDBCustomerVMContext();
                log.LogInformation($"Enter UpdateMachineLabWithGuac");

                log.LogInformation($"vmProv {ml.VMName}");
                log.LogInformation($"guacURL {guacURL}");
                log.LogInformation($"region {region}");


                var _ml = _db.MachineLabs.Where(q => q.ResourceId == ml.ResourceId).FirstOrDefault();
                var _mLogs = _db.MachineLogs.Where(q => q.ResourceId == ml.ResourceId).FirstOrDefault();
                //var _vmCustomer = _dbCustomer.VirtualMachineDetails.Where(q => q.ResourceId == vmProv.ResourceId).FirstOrDefault();
                //log.LogInformation($"_vmCustomer {_vmCustomer}");
                var _status = _db.VMStatus.Where(q => q.Id == vmStatus).FirstOrDefault();
                var courseHours = _db.VEProfileLabCreditMappings.Where(q => q.VEProfileID == _ml.VEProfileId && q.GroupID == _db.CloudLabUsers.Where(w => w.UserId == _ml.UserId).FirstOrDefault().UserGroup).FirstOrDefault().CourseHours;
                log.LogInformation($"courseHours {courseHours}");

                CloudLabsSchedules _cs = new CloudLabsSchedules();

                _ml.IsStarted = _status.Id;
                _ml.MachineStatus = _status.Status;
                _ml.RunningBy = 0;
                _ml.GuacDNS = guacURL;
                _ml.FQDN = ml.VMName + "." + region + ".cloudapp.azure.com";
                _db.Entry(_ml).State = EntityState.Modified;
                _db.SaveChanges();

                _mLogs.ModifiedDate = DateTime.UtcNow;
                _mLogs.LastStatus = _status.Status;
                _mLogs.Logs = "(" + _status.Status + ")" + DateTime.UtcNow + "---" + _mLogs.Logs;
                _db.Entry(_mLogs).State = EntityState.Modified;
                _db.SaveChanges();

                if (_dbCustomer.VirtualMachineDetails.Any(q => q.ResourceId == ml.ResourceId))
                {
                    var _vmCustomer = _dbCustomer.VirtualMachineDetails.Where(q => q.ResourceId == ml.ResourceId).FirstOrDefault();
                    _vmCustomer.DateLastModified = DateTime.UtcNow;
                    _vmCustomer.Status = 0;
                    _dbCustomer.Entry(_vmCustomer).State = EntityState.Modified;
                    _dbCustomer.SaveChanges();
                }
                else
                {
                    VirtualMachineDetails _vmCustomer = new VirtualMachineDetails();
                    _vmCustomer.ResourceId = ml.ResourceId;
                    _vmCustomer.DateLastModified = DateTime.UtcNow;
                    _vmCustomer.DateCreated = DateTime.UtcNow;
                    _vmCustomer.Status = 0;
                    _vmCustomer.VMName = ml.VMName;
                    _vmCustomer.FQDN = ml.VMName + "." + GetEnvironmentVariable("Region") + ".cloudapp.azure.com";
                    _vmCustomer.OperationId = "";
                    _dbCustomer.VirtualMachineDetails.Add(_vmCustomer);
                    _dbCustomer.SaveChanges();
                }

                if(!_db.CloudLabsSchedules.Any(q=>q.MachineLabsId == ml.MachineLabsId))
                {
                    _cs.VEProfileID = ml.VEProfileId;
                    _cs.UserId = ml.UserId;
                    _cs.TimeRemaining = courseHours * 3600;
                    _cs.LabHoursTotal = courseHours;
                    _cs.StartLabTriggerTime = null;
                    _cs.RenderPageTriggerTime = null;
                    _cs.InstructorLabHours = 7200;
                    _cs.InstructorLastAccess = null;
                    _cs.MachineLabsId = _ml.MachineLabsId;
                    _db.CloudLabsSchedules.Add(_cs);
                    _db.SaveChanges();
                }

                
            }
            catch (Exception e)
            {
                log.LogInformation($"ERROR IN UpdateMachineDatabase: {e.Message}");
            }

        }
        public static void UpdateMachineStatus(MachineLabs ml, ILogger log, int vmStatus, string status)
        {
            try
            {
                CSDBContext _db = new CSDBContext();
                CSDBCustomerVMContext _dbCustomer = new CSDBCustomerVMContext();
                log.LogInformation($"Enter UpdateMachineStatus");
                log.LogInformation($"vmProv {ml}");
                var _vmCustomer = _dbCustomer.VirtualMachineDetails.Where(q => q.ResourceId == ml.ResourceId).FirstOrDefault();

                var _ml = _db.MachineLabs.Where(q => q.ResourceId == ml.ResourceId).FirstOrDefault();
                log.LogInformation($"_ml {_ml}");
                var _mLogs = _db.MachineLogs.Where(q => q.ResourceId == ml.ResourceId).FirstOrDefault();
                log.LogInformation($"_mLogs {_mLogs}");
                var _status = _db.VMStatus.Where(q => q.Id == vmStatus).FirstOrDefault();
                log.LogInformation($"_status {_status}");

                if (status != "")
                    _ml.MachineStatus = status;
                else
                    _ml.MachineStatus = _status.Status;

                _ml.IsStarted = _status.Id;
                _ml.RunningBy = 0;
                _db.Entry(_ml).State = EntityState.Modified;
                _db.SaveChanges();


                if (status != "")
                    _mLogs.LastStatus = status;
                else
                    _mLogs.LastStatus = _status.Status;

                _mLogs.ModifiedDate = DateTime.UtcNow;
                _mLogs.Logs = "(" + _status.Status + ")" + DateTime.UtcNow + "---" + _mLogs.Logs;
                _db.Entry(_mLogs).State = EntityState.Modified;
                _db.SaveChanges();

                _vmCustomer.DateLastModified = DateTime.UtcNow;
                _vmCustomer.Status = _status.Id;
                _dbCustomer.Entry(_vmCustomer).State = EntityState.Modified;
                _dbCustomer.SaveChanges();

            }
            catch (Exception e)
            {
                log.LogInformation($"ERROR IN UpdateMachineStatus: {e.Message}");
            }

        }
        public static void UpdateMachineDeleteStatus(MachineLabs ml, ILogger log)
        {
            try
            {
                CSDBContext _db = new CSDBContext();
                CSDBCustomerVMContext _dbCustomer = new CSDBCustomerVMContext();

                log.LogInformation($"Enter UpdateMachineDeleteStatus");

                var _ml = _db.MachineLabs.Where(q => q.ResourceId == ml.ResourceId).FirstOrDefault();
                log.LogInformation($"_ml machineLabsId =  {_ml.MachineLabsId}");
                _db.MachineLabs.Remove(_ml);
                _db.SaveChanges();

                if(_db.CloudLabsSchedules.Any(q => q.MachineLabsId == _ml.MachineLabsId))
                {
                    var _cs = _db.CloudLabsSchedules.Where(q => q.MachineLabsId == _ml.MachineLabsId).FirstOrDefault();
                    log.LogInformation($"_cs scheduleId =  {_cs.CloudLabsScheduleId}");
                    _db.CloudLabsSchedules.Remove(_cs);
                    _db.SaveChanges();
                }
                if (_db.MachineLogs.Any(q => q.ResourceId == ml.ResourceId))
                {
                    var _mLogs = _db.MachineLogs.Where(q => q.ResourceId == ml.ResourceId).FirstOrDefault();
                    log.LogInformation($"_mLogs Id = {_mLogs.MachineLogsId}");
                    _db.MachineLogs.Remove(_mLogs);
                    _db.SaveChanges();
                }
                if(_dbCustomer.VirtualMachineDetails.Any(q => q.ResourceId == ml.ResourceId))
                {
                    var _vmCustomer = _dbCustomer.VirtualMachineDetails.Where(q => q.ResourceId == ml.ResourceId).FirstOrDefault();
                    _vmCustomer.DateLastModified = DateTime.UtcNow;
                    _vmCustomer.Status = -1;
                    _dbCustomer.Entry(_vmCustomer).State = EntityState.Modified;
                    _dbCustomer.SaveChanges();
                }            

            }
            catch (Exception e)
            {
                log.LogInformation($"ERROR IN UpdateMachineStatus: {e.Message}");
            }

        }
        public static void UpdateMachineFailureToDeleteStatus(MachineLabs ml, ILogger log)
        {
            try
            {
                CSDBContext _db = new CSDBContext();
                CSDBCustomerVMContext _dbCustomer = new CSDBCustomerVMContext();

                log.LogInformation($"Enter UpdateMachineFailureToDeleteStatus");
                var _vmCustomer = _dbCustomer.VirtualMachineDetails.Where(q => q.ResourceId == ml.ResourceId).FirstOrDefault();
                if (_db.MachineLabs.Any(q => q.ResourceId == ml.ResourceId))
                {
                    var _ml = _db.MachineLabs.Where(q => q.ResourceId == ml.ResourceId).FirstOrDefault();
                    log.LogInformation($"_ml machineLabsId =  {_ml.MachineLabsId}");

                    _ml.MachineStatus = "Failed To Delete";
                    _ml.IsStarted = 6;
                    _db.SaveChanges();
                }
                if (_db.CloudLabsSchedules.Any(q => q.MachineLabsId == ml.MachineLabsId))
                {
                    var _cs = _db.CloudLabsSchedules.Where(q => q.MachineLabsId == ml.MachineLabsId).FirstOrDefault();
                    log.LogInformation($"_cs scheduleId =  {_cs.CloudLabsScheduleId}");
                }
                if (_db.MachineLogs.Any(q => q.ResourceId == ml.ResourceId))
                {
                    var _mLogs = _db.MachineLogs.Where(q => q.ResourceId == ml.ResourceId).FirstOrDefault();
                    log.LogInformation($"_mLogs Id = {_mLogs.MachineLogsId}");

                    _mLogs.Logs = "Failed To Delete" +"---" +_mLogs.Logs;
                    _mLogs.LastStatus = "Failed to Delete";
                    _db.SaveChanges();
                }
                if (_db.CourseGrants.Any(q => q.UserID == ml.UserId && q.VEProfileID == ml.VEProfileId))
                {
                    var _grant = _db.CourseGrants.Where(q => q.UserID == ml.UserId && q.VEProfileID == ml.VEProfileId).FirstOrDefault();
                    log.LogInformation($"_grant Id = {_grant.AccessID}");
                }


            }
            catch (Exception e)
            {
                log.LogInformation($"ERROR IN UpdateMachineFailureToDeleteStatus: {e.Message}");
            }

        }
        public async static Task ShutDownVMWithEnvVar(ProvisionDetailsVM vmData, ILogger log) {
            try
            {
                CSDBContext _db = new CSDBContext();
                ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;

                var apiURL = _db.CloudLabUsers.Where(q => q.UserId == vmData.UserID)
                    .Join(_db.CloudLabsGroups, a => a.UserGroup, b => b.CloudLabsGroupID, (a, b) => new { a, b }).FirstOrDefault().b.CLUrl;
                var apiPrefix = _db.CloudLabUsers.Where(q => q.UserId == vmData.UserID)
                   .Join(_db.CloudLabsGroups, a => a.UserGroup, b => b.CloudLabsGroupID, (a, b) => new { a, b }).FirstOrDefault().b.ApiPrefix;

                log.LogInformation($"apiURL: {apiURL}");
                log.LogInformation($"apiPrefix: {apiPrefix}");
                log.LogInformation($"RunBook: {GetEnvironmentVariable("RunBook")}");

                HttpClient client = new HttpClient();
                //client.BaseAddress = new Uri(GetEnvironmentVariable("RunBook"));
                client.BaseAddress = new Uri(GetEnvironmentVariable("RunBook"));
                client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

                var dataV = new
                {
                    VirtualMachineName = vmData.MachineName,
                    ResourceGroupName = vmData.ResourceGroup,
                    GroupCode = apiPrefix,
                    Fqdn = apiURL.Split("https://")[1].ToLower(),
                    subscriptionId = GetEnvironmentVariable("SubscriptionId"),
                    tenantId = GetEnvironmentVariable("TenantId"),
                    ApplicationId = GetEnvironmentVariable("ClientId"),
                    ApplicationSecret = GetEnvironmentVariable("ClientSecret")
                };

                //var dataV = new
                //{
                //    VirtualMachineName = vmData.MachineName, 	//VM Name
                //    ContactPerson = vmData.ScheduledBy,						//Email
                //    SubscriptionId = GetEnvironmentVariable("SubscriptionId"),				//subscription id
                //    ApplicationId = GetEnvironmentVariable("ClientId"),					//app reg id
                //    TenantId = GetEnvironmentVariable("TenantId"),				//tenant id
                //    ApplicationKey = GetEnvironmentVariable("ClientSecret"),			//app reg key
                //    Fqdn = apiURL.Split("https://")[1].ToLower(),												//fqdn
                //    apiprefix = apiPrefix,												//apiprefix
                //    ResourceGroupName = vmData.ResourceGroup,									//rg name
                //    location = GetEnvironmentVariable("Region"),														//location
                //    computerName = vmData.MachineName												//vm computer name
                //};



                log.LogInformation($"Fqdn: {apiURL.Split("https://")[1].ToLower()}");
                var dataMsg = JsonConvert.SerializeObject(dataV);
                
                await client.PostAsync("", new StringContent(dataMsg, Encoding.UTF8, "application/json"));
               // log.LogInformation($"{s.Content.ReadAsStringAsync().Result}");
                log.LogInformation("Calling Runbook");

            }
            catch(Exception ex)
            {
                log.LogInformation($"Error " + ex.Message);
            }                    
        }
        public static string AddMachineToDatabase(string machineName, TenantDetails _tenantDetails, string Username, string pass, int VETypeID, string Fqdn, ILogger log)
        {
            try
            {
                string Password = pass.Replace(@"\", @"\\");

                log.LogInformation("Enter Guac");
                var guacDatabase = new MySqlConnection(_tenantDetails.GuacConnection);
                var Environment = _tenantDetails.EnvironmentCode.Trim() == "D" ? "Dev" : _tenantDetails.EnvironmentCode.Trim() == "Q" ? "QA" : _tenantDetails.EnvironmentCode.Trim() == "U" ? "Demo" : "Prod";

                guacDatabase.Open();
                log.LogInformation("Open Guac");
                string selectQuery = "";
                string protocol = "rdp";

                selectQuery = $"SELECT connection_id FROM guacamole_connection WHERE connection_name like '%{machineName}%'";
                var MySqlCommandConn = new MySqlCommand(selectQuery, guacDatabase);
                var dataReader = MySqlCommandConn.ExecuteReader();

                log.LogInformation("Read Guac");
                dataReader.Read();

                if (!dataReader.HasRows)
                {
                    log.LogInformation("No Rows Guac");
                    dataReader.Close();

                    var insertQuery = "INSERT INTO guacamole_connection (connection_name, protocol, max_connections, max_connections_per_user) " +
                        $"VALUES (\'{machineName}-{protocol}\', \'{protocol}\', \'5\', \'4\')";

                    var insertCommand = new MySqlCommand(insertQuery, guacDatabase);

                    log.LogInformation("insertQuery Guac");
                    insertCommand.ExecuteNonQuery();

                    log.LogInformation("Execute Guac");
                    selectQuery = $"SELECT connection_id FROM guacamole_connection WHERE connection_name = '{machineName}-{protocol}'";

                    var MySqlCommand = new MySqlCommand(selectQuery, guacDatabase);

                    log.LogInformation("Select Guac");
                    var dataReaderIns = MySqlCommand.ExecuteReader();

                    log.LogInformation("Execute select Guac");
                    dataReaderIns.Read();
                    var connectionId = Convert.ToInt32(dataReaderIns["connection_id"]);

                    log.LogInformation($"connectionId {connectionId}");
                    dataReaderIns.Close();

                    var guacUrlHostName = _tenantDetails.GuacamoleURL;
                    log.LogInformation($"guacUrlHostName {guacUrlHostName}");
                    guacUrlHostName = guacUrlHostName.Replace("https://", string.Empty);

                    log.LogInformation($"guacUrlHostName again {guacUrlHostName}");
                    var insertParamsQuery = string.Empty;

                    insertParamsQuery =
                        "INSERT INTO guacamole_connection_parameter (connection_id, parameter_name, parameter_value) " +
                        $"VALUES ({connectionId}, 'hostname', '{Fqdn}'), " +
                        $"({connectionId}, 'ignore-cert', 'true'), " +
                        $"({connectionId}, 'password', '{Password}'), " +
                        $"({connectionId}, 'security', 'nla'), " +
                        $"({connectionId}, 'port', '3389'), " +
                        $"({connectionId}, 'enable-wallpaper', 'true'), " +
                        $"({connectionId}, 'username', '{Username}')";

                    MySqlCommand insertParamsCommand = new MySqlCommand();
                    log.LogInformation($"insertParamsCommand");
                    //windows
                    if (VETypeID == 1 || VETypeID == 3 || VETypeID == 9 || VETypeID == 10)
                    {
                        insertParamsCommand = new MySqlCommand(insertParamsQuery, guacDatabase);
                        log.LogInformation($"VETYPE {VETypeID}");
                    }
                    //linux
                    else if (VETypeID == 2 || VETypeID == 4)
                    {
                        insertParamsQuery = "INSERT INTO guacamole_connection_parameter (connection_id, parameter_name, parameter_value) " +
                        $"VALUES ({connectionId}, 'hostname', '{Fqdn}'), " +
                        $"({connectionId}, 'ignore-cert', 'true'), " +
                        $"({connectionId}, 'password', '{Password}'), " +
                        $"({connectionId}, 'security', ''), " +
                        $"({connectionId}, 'port', '3389'), " +
                        $"({connectionId}, 'enable-wallpaper', 'true'), " +
                        $"({connectionId}, 'username', '{Username}')";

                        insertParamsCommand = new MySqlCommand(insertParamsQuery, guacDatabase);
                        
                        log.LogInformation($"VETYPE {VETypeID}");

                    }

                    insertParamsCommand.ExecuteNonQuery();
                    log.LogInformation($"insertParamsCommand Execute");
                    selectQuery = $"SELECT entity_id FROM guacamole_entity WHERE name = '{Environment}'";
                    MySqlCommand = new MySqlCommand(selectQuery, guacDatabase);
                    var dataReader2 = MySqlCommand.ExecuteReader();
                    
                    log.LogInformation($"MySqlCommand Execute");
                    dataReader2.Read();
                    log.LogInformation($"dataReader2 Read");
                    var userId = Convert.ToInt32(dataReader2["entity_id"]);
                    log.LogInformation($"userId {userId}");
                    dataReader2.Close();

                    var insertPermissionQuery = string.Format("INSERT INTO guacamole_connection_permission(entity_id, connection_id, permission) VALUES ({1},{0}, 'READ')", connectionId, userId);

                    var insertPermissionCommand = new MySqlCommand(insertPermissionQuery, guacDatabase);

                    insertPermissionCommand.ExecuteNonQuery();

                    log.LogInformation($"insertPermissionCommand Execute");
                    var clientId = new string[3] { connectionId.ToString(), "c", "mysql" };

                    var bytes = Encoding.UTF8.GetBytes(string.Join("\0", clientId));
                    var connectionString = Convert.ToBase64String(bytes);

                    var guacUrl =
                        $"{_tenantDetails.GuacamoleURL}/guacamole/#/client/{connectionString}?username={Environment}&password=pr0v3byd01n6!";

                    log.LogInformation($"guacUrl {guacUrl}");

                    //var guacamoleInstance = new GuacamoleInstance()
                    //{
                    //    Connection_Name = hostName,
                    //    Hostname = guacUrlHostName,
                    //    Url = guacUrl
                    //};

                    guacDatabase.Close();

                    return guacUrl;
                }
                else
                {
                    dataReader.Close();

                    selectQuery = $"SELECT connection_id FROM guacamole_connection WHERE connection_name = \'{machineName}-{protocol}\'";

                    var MySqlCommand = new MySqlCommand(selectQuery, guacDatabase);

                    var dataReaderIns = MySqlCommand.ExecuteReader();

                    dataReaderIns.Read();
                    var connectionId = Convert.ToInt32(dataReaderIns["connection_id"]);

                    dataReaderIns.Close();

                    var guacUrlHostName = _tenantDetails.GuacamoleURL;
                    guacUrlHostName = guacUrlHostName.Replace("https://", string.Empty);

                    var updateParamsQuery = string.Empty;
                    var updateParamPassQuery = string.Empty;


                    //windows, gcp
                    if (VETypeID == 1 || VETypeID == 3 || VETypeID == 9 || VETypeID == 10)
                    {
                        updateParamsQuery = "UPDATE guacamole_connection_parameter SET parameter_value = '" + Fqdn + "' WHERE connection_Id = " + connectionId + " and parameter_name = 'hostname'";

                        MySqlCommand updateParamsCommand = new MySqlCommand();

                        updateParamsCommand = new MySqlCommand(updateParamsQuery, guacDatabase);
                        updateParamsCommand.ExecuteNonQuery();


                        updateParamPassQuery = "UPDATE guacamole_connection_parameter SET parameter_value = \"" + Password + "\" WHERE connection_Id = " + connectionId + " and parameter_name = 'password'";

                        MySqlCommand updateParamPassCommand = new MySqlCommand();

                        updateParamPassCommand = new MySqlCommand(updateParamPassQuery, guacDatabase);
                        updateParamPassCommand.ExecuteNonQuery();
                    }
                    //linux
                    else if (VETypeID == 2 || VETypeID == 4)
                    {
                        updateParamsQuery = "UPDATE guacamole_connection_parameter SET parameter_value = '" + Fqdn + "' WHERE connection_Id = "
                            + connectionId + " and parameter_name = 'hostname'";

                        MySqlCommand updateParamsCommand = new MySqlCommand();

                        updateParamsCommand = new MySqlCommand(updateParamsQuery, guacDatabase);
                        updateParamsCommand.ExecuteNonQuery();

                        var updateParamsQuery1 = "UPDATE guacamole_connection_parameter SET parameter_value = '" + "" + "' WHERE connection_Id = "
                            + connectionId + " and parameter_name = 'security'";

                        MySqlCommand updateParamsCommand1 = new MySqlCommand();

                        updateParamsCommand1 = new MySqlCommand(updateParamsQuery1, guacDatabase);
                        updateParamsCommand1.ExecuteNonQuery();

                    }

                    var clientId = new string[3] { connectionId.ToString(), "c", "mysql" };

                    var bytes = Encoding.UTF8.GetBytes(string.Join("\0", clientId));
                    var connectionString = Convert.ToBase64String(bytes);

                    var guacUrl =
                        $"{_tenantDetails.GuacamoleURL}/guacamole/#/client/{connectionString}?username={Environment}&password=pr0v3byd01n6!";


                    guacDatabase.Close();

                    return guacUrl;

                }
            }
            catch (Exception ex)
            {
                var x = ex.InnerException;
                log.LogInformation($"Error Creating Guac {ex.Message}");
                return "";
            }

        }
        public static VirtualMachineSizeTypes Size(string size)
        {
            switch (size)
            {
                case "Standard_B2s":
                    return VirtualMachineSizeTypes.StandardB2s;
                case "Standard_B2ms":
                    return VirtualMachineSizeTypes.StandardB2ms;
                case "Standard_B4ms":
                    return VirtualMachineSizeTypes.StandardB4ms;
                case "Standard_D1_v2":
                    return VirtualMachineSizeTypes.StandardD1V2;
                case "Standard_D2s_v3":
                    return VirtualMachineSizeTypes.StandardD2sV3;
                case "Standard_D4s_v3":
                    return VirtualMachineSizeTypes.StandardD4sV3;
                case "Standard_F2s_v2":
                    return VirtualMachineSizeTypes.StandardF2sV2;
                case "Standard_F4s_v2":
                    return VirtualMachineSizeTypes.StandardF4sV2;
                case "Standard_D4_v3":
                    return VirtualMachineSizeTypes.StandardD4V3;
                case "Standard_D8_v3":
                    return VirtualMachineSizeTypes.StandardD8sV3;
                case "Standard_NV6":
                    return VirtualMachineSizeTypes.StandardNV6;
                case "Standard_E4s_v3":
                    return VirtualMachineSizeTypes.StandardE4sV3;
                case "Standard_A2_v2":
                    return VirtualMachineSizeTypes.StandardA2V2;
                default:
                    return VirtualMachineSizeTypes.StandardB2ms;
            };
        }
        public static Region Location(string location)
        {
            switch (location)
            {
                case "eastasia":
                    return Region.AsiaEast;
                case "southeastasia":
                    return Region.AsiaSouthEast;
                case "centralus":
                    return Region.USCentral;
                case "eastus":
                    return Region.USEast;
                case "eastus2":
                    return Region.USEast2;
                case "westus":
                    return Region.USWest;
                case "northcentralus":
                    return Region.USNorthCentral;
                case "southcentralus":
                    return Region.USSouthCentral;
                case "northeurope":
                    return Region.EuropeNorth;
                case "westeurope":
                    return Region.EuropeWest;
                case "japanwest":
                    return Region.JapanWest;
                case "japaneast":
                    return Region.JapanEast;
                case "brazilsouth":
                    return Region.BrazilSouth;
                case "australiaeast":
                    return Region.AustraliaEast;
                case "australiasoutheast":
                    return Region.AustraliaSouthEast;
                case "southindia":
                    return Region.IndiaSouth;
                case "centralindia":
                    return Region.IndiaCentral;
                case "westindia":
                    return Region.IndiaWest;
                case "canadacentral":
                    return Region.CanadaCentral;
                case "canadaeast":
                    return Region.CanadaEast;
                case "uksouth":
                    return Region.UKSouth;
                case "ukwest":
                    return Region.UKWest;
                case "westcentralus":
                    return Region.USWestCentral;
                case "westus2":
                    return Region.USWest2;
                case "koreacentral":
                    return Region.KoreaCentral;
                case "koreasouth":
                    return Region.KoreaSouth;
                case "francecentral":
                    return Region.FranceCentral;
                case "francesouth":
                    return Region.FranceSouth;
                case "australiacentral":
                    return Region.AustraliaCentral;
                case "australiacentral2":
                    return Region.AustraliaCentral2;
                case "uaecentral":
                    return Region.UAECentral;
                case "uaenorth":
                    return Region.UAENorth;
                case "southafricanorth":
                    return Region.SouthAfricaNorth;
                case "southafricawest":
                    return Region.SouthAfricaWest;
                case "switzerlandnorth":
                    return Region.SwitzerlandNorth;
                case "switzerlandwest":
                    return Region.SwitzerlandWest;
                case "germanynorth":
                    return Region.GermanyNorth;
                case "germanywestcentral":
                    return Region.GermanyWestCentral;
                case "norwaywest":
                    return Region.NorwayWest;
                case "norwayeast":
                    return Region.NorwayEast;
                case "uswest3":
                    return Region.USWest3;
                default:
                    return Region.AsiaEast;
            }
        }
        public static void UpdateMachineGCPDBGuac(MachineLabs ml, string guacURL, VMPayload data, ILogger log)
        {
            try
            {
                CSDBContext _db = new CSDBContext();
                CSDBCustomerVMContext _dbCustomer = new CSDBCustomerVMContext();
                MachineLogs _mLogs = new MachineLogs();
                log.LogInformation($"Enter UpdateMachineLabGCPWithGuac");

                log.LogInformation($"vmProv {ml}");
                log.LogInformation($"guacURL {guacURL}");
                

                var _ml = _db.MachineLabs.Where(q => q.ResourceId == ml.ResourceId).FirstOrDefault();
                
                var courseHours = _db.VEProfileLabCreditMappings.Where(q => q.VEProfileID == _ml.VEProfileId && q.GroupID == _db.CloudLabUsers.Where(w => w.UserId == _ml.UserId).FirstOrDefault().UserGroup).FirstOrDefault().CourseHours;

                CloudLabsSchedules _cs = new CloudLabsSchedules();

                _ml.ResourceId = data.data.instance_id;
                _ml.Password = ml.Password;
                _ml.IsStarted = 4;
                _ml.Username = data.data.user;
                _ml.MachineStatus = "Provisioning";
                _ml.RunningBy = 0;
                _ml.GuacDNS = guacURL;
                _ml.FQDN = data.data.nat_i_p;
                //_ml.DateProvision = DateTime.UtcNow.ToString();
                _db.Entry(_ml).State = EntityState.Modified;
                _db.SaveChanges();

                if (_db.MachineLogs.Any(q => q.ResourceId == ml.ResourceId))
                {
                    _mLogs = _db.MachineLogs.Where(q => q.ResourceId == ml.ResourceId).FirstOrDefault();
                    _mLogs.ModifiedDate = DateTime.UtcNow;
                    _mLogs.LastStatus = "Provisioning";
                    _mLogs.Logs = "(Provisioning)" + DateTime.UtcNow + "---" + _mLogs.Logs;
                    _db.Entry(_mLogs).State = EntityState.Modified;
                    _db.SaveChanges();
                }
                else
                {
                    _mLogs.ModifiedDate = DateTime.UtcNow;
                    _mLogs.LastStatus = "Provisioning";
                    _mLogs.Logs = "(Provisioning)" + DateTime.UtcNow + "---" + _mLogs.Logs;
                    _mLogs.ResourceId = data.data.instance_id;
                    _mLogs.RequestId = null;

                    _db.MachineLogs.Add(_mLogs);
                    _db.SaveChanges();
                }

                if (_dbCustomer.VirtualMachineDetails.Any(q => q.ResourceId == ml.ResourceId))
                {
                    var _vmCustomer = _dbCustomer.VirtualMachineDetails.Where(q => q.ResourceId == ml.ResourceId).FirstOrDefault();
                    _vmCustomer.DateLastModified = DateTime.UtcNow;
                    _vmCustomer.Status = 0;
                    _dbCustomer.Entry(_vmCustomer).State = EntityState.Modified;
                    _dbCustomer.SaveChanges();
                }
                else
                {
                    VirtualMachineDetails _vmCustomer = new VirtualMachineDetails();
                    _vmCustomer.ResourceId = data.data.instance_id;
                    _vmCustomer.DateLastModified = DateTime.UtcNow;
                    _vmCustomer.DateCreated = DateTime.UtcNow;
                    _vmCustomer.Status = 0;
                    _vmCustomer.VMName = ml.VMName;
                    _vmCustomer.FQDN = data.data.nat_i_p;
                    _vmCustomer.OperationId = "";
                    _dbCustomer.VirtualMachineDetails.Add(_vmCustomer);
                    _dbCustomer.SaveChanges();
                }

                if (!_db.CloudLabsSchedules.Any(q => q.MachineLabsId == ml.MachineLabsId))
                {
                    _cs.VEProfileID = ml.VEProfileId;
                    _cs.UserId = ml.UserId;
                    _cs.TimeRemaining = courseHours * 3600;
                    _cs.LabHoursTotal = courseHours;
                    _cs.StartLabTriggerTime = null;
                    _cs.RenderPageTriggerTime = null;
                    _cs.InstructorLabHours = 7200;
                    _cs.InstructorLastAccess = null;
                    _cs.MachineLabsId = _ml.MachineLabsId;
                    _db.CloudLabsSchedules.Add(_cs);
                    _db.SaveChanges();
                }


            }
            catch (Exception e)
            {
                log.LogInformation($"ERROR IN UpdateMachineDatabase: {e.Message}");
            }

        }
        public static void UpdateMachineGCP(MachineLabs ml, ILogger log, string operation, VMPayload data, TenantDetails tenants)
        {
            CSDBContext _db = new CSDBContext();
            CSDBCustomerVMContext _dbCustomer = new CSDBCustomerVMContext();

            var _ml = _db.MachineLabs.Where(q => q.ResourceId == ml.ResourceId).FirstOrDefault();
            var _mLogs = _db.MachineLogs.Where(q => q.ResourceId == ml.ResourceId).FirstOrDefault();
            var _vmCustomer = _dbCustomer.VirtualMachineDetails.Where(q => q.ResourceId == ml.ResourceId).FirstOrDefault();

            if(operation.ToLower() == "shutdown" || operation.ToLower() == "deallocated")
            {
                _ml.IsStarted = 0;
                _ml.MachineStatus = "Shutdown";
                _ml.RunningBy = 0;
                _db.Entry(_ml).State = EntityState.Modified;
                _db.SaveChanges();
            }
            else if (operation.ToLower() == "stopping")
            {
                _ml.IsStarted = 2;
                _ml.MachineStatus = "Stopping";
                _ml.RunningBy = 0;               
                _db.Entry(_ml).State = EntityState.Modified;
                _db.SaveChanges();
            }
            else if (operation.ToLower() == "running")
            {
                _ml.IsStarted = 1;
                _ml.MachineStatus = "Running";
                _ml.RunningBy = 1;
                _ml.FQDN = data.data.nat_i_p;
                var guacDNS = AddMachineToDatabase(ml.VMName, tenants, data.data.user, data.data.vm_pass, 10, data.data.nat_i_p, log);
                _ml.GuacDNS = guacDNS;
                _ml.Password = Encrypt(data.data.vm_pass);
                _db.Entry(_ml).State = EntityState.Modified;
                _db.SaveChanges();
            }

            _mLogs.ModifiedDate = DateTime.UtcNow;
            _mLogs.LastStatus = operation;
            _mLogs.Logs = "(" + operation + ")" + DateTime.UtcNow + "---" + _mLogs.Logs;
            _db.Entry(_mLogs).State = EntityState.Modified;
            _db.SaveChanges();

            _vmCustomer.DateLastModified = DateTime.UtcNow;
            _vmCustomer.Status = 0;
            _vmCustomer.OperationId = "";
            _dbCustomer.Entry(_vmCustomer).State = EntityState.Modified;
            _dbCustomer.SaveChanges();

        }
        public static JObject GetJObject(byte[] resourceTemplate)
        {
            Stream stream = new MemoryStream(resourceTemplate);
            StreamReader template = new StreamReader(stream);
            JsonTextReader reader = new JsonTextReader(template);
            return (JObject)JToken.ReadFrom(reader);
        }
        public static void Email(string htmlString, ILogger log, MailMessage message, string clientName, string attachmentPath = "")
        {
            string SendGridKey = GetEnvironmentVariable("SendGridKey");
            string SendGridName = GetEnvironmentVariable("SendGridName");
            try
            {
                SmtpClient smtp = new SmtpClient();
                message.Subject = $"Logs {clientName}";
                if (!attachmentPath.Equals(string.Empty))
                {
                    message.Attachments.Add(new Attachment(attachmentPath));
                }
                message.IsBodyHtml = true;
                message.Body = htmlString;
                smtp.Port = 587;
                smtp.Host = "smtp.sendgrid.net";
                smtp.EnableSsl = true;
                smtp.UseDefaultCredentials = false;
                smtp.Credentials = new NetworkCredential(SendGridName, SendGridKey);
                smtp.DeliveryMethod = SmtpDeliveryMethod.Network;
                smtp.Send(message);
            }
            catch (Exception e)
            {
                log.LogInformation(e.Message);
            }
        }
        public static void SendMail(MemoryStream ms, string subject, string body)
        {
            string[] TO = JsonConvert.DeserializeObject<string[]>(GetEnvironmentVariable("TO"));
            string[] CC = JsonConvert.DeserializeObject<string[]>(GetEnvironmentVariable("CC"));

            MailMessage mailMsg = new MailMessage();
            foreach (string email in TO)
                mailMsg.To.Add(new MailAddress(email));

            foreach (string email in CC)
                mailMsg.CC.Add(new MailAddress(email));

            //foreach (string email in CC)  
            //    message.To.Add(new MailAddress(email));
            string filename = ClientEnvironment + "-" +DateTime.UtcNow.ToShortDateString() + ".xlsx";          

            mailMsg.From = new MailAddress(SendGridSender, "CloudSwyft Global Systems Inc");
            mailMsg.Subject = subject + "-" + ClientEnvironment;
            mailMsg.AlternateViews.Add(AlternateView.CreateAlternateViewFromString(body, null, MediaTypeNames.Text.Html));
            mailMsg.Attachments.Add(new Attachment(ms, filename, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet"));
            mailMsg.Headers.Add("Priority", "Urgent");
            SmtpClient smtpClient = new SmtpClient("smtp.sendgrid.net", Convert.ToInt32(587));
            NetworkCredential credentials = new NetworkCredential(SendGridUsername, SendGridPassword);
            smtpClient.Credentials = credentials;
            
            smtpClient.Send(mailMsg);
        }
        public static void UpdateVMNotExist(MachineLabs ml)
        {
            CSDBContext _db = new CSDBContext();
            CSDBCustomerVMContext _dbCustomer = new CSDBCustomerVMContext();

            var _vmCustomer = _dbCustomer.VirtualMachineDetails.Where(q => q.ResourceId == ml.ResourceId).FirstOrDefault();


            if(_db.MachineLabs.Any(q => q.ResourceId == ml.ResourceId))
            {
                var _ml = _db.MachineLabs.Where(q => q.ResourceId == ml.ResourceId).FirstOrDefault();
                _ml.IsStarted = 7;
                _ml.MachineStatus = "Deleted";
                _db.Entry(_ml).State = EntityState.Modified;
                _db.SaveChanges();
            }
            if (_db.MachineLogs.Any(q => q.ResourceId == ml.ResourceId))
            {
                var _mlogs = _db.MachineLogs.Where(q => q.ResourceId == ml.ResourceId).FirstOrDefault();
                _mlogs.LastStatus = "Deleted, VM not Exist";
                _mlogs.Logs = "(Deleted, VM not Exist)" + DateTime.UtcNow + "---" + _mlogs.Logs;
                _mlogs.ModifiedDate = DateTime.UtcNow;

                _db.Entry(_mlogs).State = EntityState.Modified;
                _db.SaveChanges();
            }
        }

        public static void UpdateTimeSchedMachineLab(string resourceId, int statusId, string machineStatus)
        {
            try
            {
                CSDBContext _db = new CSDBContext();
                CSDBCustomerVMContext _dbCustomer = new CSDBCustomerVMContext();

                var _ml = _db.MachineLabs.Where(q => q.ResourceId == resourceId).FirstOrDefault();
                var _mLogs = _db.MachineLogs.Where(q => q.ResourceId == resourceId).FirstOrDefault();

                _ml.IsStarted = statusId;
                _ml.MachineStatus = machineStatus;
                _ml.RunningBy = 1;
                _db.Entry(_ml).State = EntityState.Modified;
                _db.SaveChanges();

                _mLogs.ModifiedDate = DateTime.UtcNow;
                _mLogs.LastStatus = machineStatus;
                _mLogs.Logs = machineStatus + DateTime.UtcNow + "---" + _mLogs.Logs;
                _db.Entry(_mLogs).State = EntityState.Modified;
                _db.SaveChanges();

            }
            catch (Exception e)
            {
                var s = e.Message;
            }
        }
    }
}
