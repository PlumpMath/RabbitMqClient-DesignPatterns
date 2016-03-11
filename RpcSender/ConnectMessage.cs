using System;
using System.Diagnostics;

namespace RpcSender
{
    class ConnectMessage
    {
        public ConnectMessage(string groupName, int applicationTimeout, int groupTimeout, string applicationName)
        {
            Timestamp = DateTime.UtcNow;
            Groupname = groupName;
            ApplicationTimeout = applicationTimeout;
            GroupTimeout = groupTimeout;
            ApplicationName = applicationName;

        }

        public DateTime Timestamp { get; }

        public int Pid => Process.GetCurrentProcess().Id;
        public string MachineName => Environment.MachineName;

        public string ApplicationName { get; }
        public string Groupname { get; }
        public int ApplicationTimeout { get; }
        public int GroupTimeout { get; }
    }
}
