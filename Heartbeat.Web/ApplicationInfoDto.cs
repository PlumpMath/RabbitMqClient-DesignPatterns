using System;

namespace Heartbeat.Web
{
    [Serializable]
    public class ApplicationInfoDto
    {
        public int CurrentApplicationRetries { get; set; }
        public int CurrentGroupRetries { get; set; }

        public string ListenToQueue { get; set; }

        public string PublishQueue { get; set; }

        public string CorrelationId { get; set; }

        public DateTime LastUpdated { get; set; }

        public string ApplicationName { get; set; }
        public int ApplicationTimeout { get; set; }
        public int ApplicationRetries { get; set; }
        public string GroupName { get; set; }
        public int GroupTimeout { get; set; }
        public int GroupRetries { get; set; }

    }
}