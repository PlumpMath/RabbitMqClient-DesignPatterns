using System;
using System.Web.Http;

namespace Heartbeat.Web.Controllers
{
    public class ClientController : ApiController
    {
        public String GetClients()
        {
            return "Hello World";
        }
    }
}
