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
            String response = messagingService.SendRpcConnectMessageToQueue(Environment.MachineName + "_" +Process.GetCurrentProcess().Id, TimeSpan.FromMinutes(1));
            Console.WriteLine("Response: {0}", response);
            

           messagingService.ReceiveRpcPongMessage();

        }
    }
}
