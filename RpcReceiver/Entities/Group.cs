using System.Collections.Generic;

namespace RpcReceiver.Entities
{
    public class Group
    {
        public Group()
        {
            Applications = new List<Application>();
        }

        public int Id { get; set; }
        public string GroupName { get; set; }
        public int GroupTimeout { get; set; }
        public int GroupRetries { get; set; }
        public int CurrentGroupRetries { get; set; }
        public ICollection<Application> Applications { get; set; }
    }
}
