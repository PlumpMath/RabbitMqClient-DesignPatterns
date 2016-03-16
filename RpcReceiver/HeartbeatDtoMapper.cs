using RpcReceiver.Entities;

namespace RpcReceiver
{
    public static class HeartbeatDtoMapper
    {
        public static Application ApplicationMapper(ApplicationInfoDto applicationInfoDto)
        {
            return new Application
            {
                ApplicationName = applicationInfoDto.ApplicationName,
                ApplicationRetries = applicationInfoDto.ApplicationRetries,
                ApplicationTimeout = applicationInfoDto.ApplicationTimeout,
                CurrentApplicationRetries = applicationInfoDto.ApplicationRetries,
                ReplyTo = applicationInfoDto.ReplyTo,
                CorrelationId = applicationInfoDto.CorrelationId,
                PublishQueue = applicationInfoDto.PublishQueue,
                LastUpdated = applicationInfoDto.LastUpdated                
            };
        }

        public static Group GroupMapper(ApplicationInfoDto applicationInfoDto, Application app)
        {
            var group =  new Group
            {
                GroupName = applicationInfoDto.GroupName,
                CurrentGroupRetries = applicationInfoDto.CurrentGroupRetries,
                GroupRetries = applicationInfoDto.GroupRetries,
                GroupTimeout = applicationInfoDto.GroupTimeout
            };

            group.Applications.Add(app);
            return group;
        }
         
    }
}