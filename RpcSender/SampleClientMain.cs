using System;
using System.Diagnostics;

namespace RpcSender
{
    class SampleClientMain
    {
        static void Main(string[] args)
        {
            var s = new SampleClientService();
            RunRpcMessageDemo(s);
        }

        private static void RunRpcMessageDemo(SampleClientService messagingService)
        {
            messagingService.SendRpcConnectMessageToQueue();
        }
    }
}
