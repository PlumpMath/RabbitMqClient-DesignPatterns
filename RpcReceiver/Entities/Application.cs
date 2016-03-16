using System;

namespace RpcReceiver.Entities
{
    public class Application
    {
        public int Id { get; set; }
        public int CurrentApplicationRetries { get; set; }

        public string ReplyTo { get; set; }

        public string PublishQueue { get; set; }

        public string CorrelationId { get; set; }

        public DateTime LastUpdated { get; set; }

        public string ApplicationName { get; set; }
        public int ApplicationTimeout { get; set; }
        public int ApplicationRetries { get; set; }
    }
}