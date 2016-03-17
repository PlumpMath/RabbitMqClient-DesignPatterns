using System;
using System.Net;
using System.Net.Http;
using System.Web.Http;

namespace Heartbeat.Web.Controllers
{
    [RoutePrefix("api/heartbeat")]
    public class HearbeatController:ApiController
    {


        public HttpResponseMessage GetHeartbeat()
        {
            return Request.CreateResponse(HttpStatusCode.OK, new { Group = HeartbeatService.GroupApps, Applications= HeartbeatService.ApplicationSubscriptions});

        }
    }
}