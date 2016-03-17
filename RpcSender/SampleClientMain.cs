using System;
using System.Diagnostics;

namespace RpcSender
{
    class SampleClientMain
    {
        static void Main(string[] args)
        {
            HeartbeatClientService.Start();
            Console.ReadKey();
            HeartbeatClientService.Stop();
        }
    }
}
