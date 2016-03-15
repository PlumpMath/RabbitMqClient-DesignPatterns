using System;
using System.Diagnostics;

namespace Heartbeat.Contracts
{
    public class ConnectMessage
    {
        public DateTime Timestamp { get; set; }
        public string ApplicationName { get; set; }
        public int ApplicationTimeout { get; set; }
        public int ApplicationRetries { get; set; }
        public string GroupName { get; set; }
        public int GroupTimeout { get; set; }
        public int GroupRetries { get; set; }
    }
}
