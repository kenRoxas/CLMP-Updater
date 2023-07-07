using System;
using System.Collections.Generic;
using System.Text;

namespace VMWAProvision.Model
{
    class GCPModel
    {
    }

    public class VMPayload
    {
        public VMInfoPayload data { get; set; }
    }
    public class VMInfoPayload
    {
        public string instance_id { get; set; }
        public string instance_name { get; set; }
        public string user { get; set; }
        public string vm_pass { get; set; }
        public string status { get; set; }
        public string project_id { get; set; }
        public string zone { get; set; }
        public string image_project { get; set; }
        public string image_os { get; set; }
        public string disk_size_gb { get; set; }
        public string network_i_p { get; set; }
        public string nat_i_p { get; set; }
        public string disk_os { get; set; }
        public string machine_type { get; set; }
        public string network { get; set; }
        public string subnetwork { get; set; }
    }
    public class VMHeartBeat
    {
        public VMHeartBeatPayload data { get; set; }
    }
    public class VMHeartBeatPayload
    {
        public string id { get; set; }
        public DateTime created { get; set; }
        public DateTime modified { get; set; }
        public string source_object_id { get; set; }
        public int minutes_rendered { get; set; }
        public int source_content_type { get; set; }
        public int user { get; set; }
       
    }
}
