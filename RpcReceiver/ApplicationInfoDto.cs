using System;
using System.Timers;
using Heartbeat.Contracts;
using Igc.RabbitMq;

namespace RpcReceiver
{
    public class ApplicationInfoDto
    {
        public int CurrentApplicationRetries { get; set; }
        public int CurrentGroupRetries { get; set; }

        public string ReplyTo { get; set; }

        public Timer Timer { get; set; }

        public string PublishQueue { get; set; }

        public string CorrelationId { get; set; }

        public IPublisher Publisher { get; set; }

        public DateTime LastUpdated { get; set; }

        public string ApplicationName { get; set; }
        public int ApplicationTimeout { get; set; }
        public int ApplicationRetries { get; set; }
        public string GroupName { get; set; }
        public int GroupTimeout { get; set; }
        public int GroupRetries { get; set; }

    }
}