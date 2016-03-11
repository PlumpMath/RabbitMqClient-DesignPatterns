

namespace RpcReceiver
{
    class HeaertBeatMain
    {
        static void Main(string[] args)
        {
            HeartbeatService s = new HeartbeatService();
            s.ReceiveRpcConnectMessage();
        }
    }
}
