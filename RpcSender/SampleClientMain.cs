using System;
using System.Diagnostics;

namespace RpcSender
{
    class SampleClientMain
    {
        static void Main(string[] args)
        {
            
            //messagingService.SetUpExchangeAndQueueForRpcDemo(model);
            SampleClientService s = new SampleClientService();
            //s.SetUpExchangeAndQueueForRpcDemo();
            RunRpcMessageDemo(s);
        }

        private static void RunRpcMessageDemo(SampleClientService messagingService)
        {
            messagingService.SendRpcConnectMessageToQueue(Environment.MachineName + "_" +Process.GetCurrentProcess().Id);
           //messagingService.ReceiveRpcPongMessage();

        }
    }
}
