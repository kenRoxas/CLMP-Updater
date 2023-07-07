//using Microsoft.AspNetCore.Http;
//using Microsoft.Azure.Management.Graph.RBAC.Fluent.Models;
//using Microsoft.Azure.Management.Graph.RBAC.Fluent;
//using System;
//using System.Collections.Generic;
//using System.Security;
//using System.Text;
//using System.Management.Automation;
//using System.Management.Automation.Runspaces;
//using System.IO;
//using Newtonsoft.Json.Linq;
//using Newtonsoft.Json;
//using System.Reflection;

//namespace VMWAProvision.Helpers
//{
//    public static class GuestAzureWindowsHelper
//    {
//        public static string GuestAzureHelper()
//        {
//            string Username = "valerie";
//            string Password = "Password1!";

//            //var securestring = new SecureString();

//            //foreach (Char c in Password)
//            //{
//            //    securestring.AppendChar(c);
//            //}

//            //PSCredential creds = new PSCredential(Username, securestring);
//            //WSManConnectionInfo connectionInfo = new WSManConnectionInfo();

//            //connectionInfo.ComputerName = "machineName";
//            //connectionInfo.Credential = creds;




//            using (Stream st = new MemoryStream(Properties.Resources.AddGuestUser))
//            {
//                using (StreamReader sr = new StreamReader(st))
//                {
//                    string script = sr.ReadToEnd();

//                    //using (PowerShell ps = PowerShell.Create())
//                    //{
//                    Runspace runspace = RunspaceFactory.CreateRunspace();

//                    using (Pipeline pipeline = runspace.CreatePipeline())
//                    { 
//                        Command cmd = new Command(script, true);

//                        CommandParameter userParam = new CommandParameter("Username", Username);
//                        cmd.Parameters.Add(userParam);
//                        CommandParameter officeParam = new CommandParameter("Password", Password);
//                        cmd.Parameters.Add(officeParam);
//                        //Add Parameters

//                        ps.Commands.AddCommand(cmd);
//                        ps.Invoke();
//                    }
//                }
//            }

//            return "Ok";

//            //String psProg = File.ReadAllText(@"D:\Repo\VMUpdater\SWO-FA-Provisioning\AddGuestUser.ps1");

//            //Runspace runspace = RunspaceFactory.CreateRunspace(connectionInfo);
//            //runspace.Open();
//            //using (PowerShell ps = PowerShell.Create())
//            //{
//            //    ps.Runspace = runspace;
//            //    ps.AddScript(psProg);
//            //    ps.AddArgument(@"Argument1");
//            //    StringBuilder sb = new StringBuilder();
//            //    try
//            //    {
//            //        var results = ps.Invoke();
//            //        foreach (var x in results)
//            //        {
//            //            sb.AppendLine(x.ToString());
//            //        }
//            //        return "Ok";
//            //    }
//            //    catch (Exception e)
//            //    {
//            //        return e.Message;
//            //    }
//            //}
//            //runspace.Close();

//        }
//    }
//}
