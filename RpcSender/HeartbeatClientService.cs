using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Configuration;
using System.Diagnostics;
using System.Threading.Tasks;
using Heartbeat.Contracts;
using Igc.RabbitMq;
using Igc.RabbitMq.Consumption;
using NLog;

namespace RpcSender
{
    public static class HeartbeatClientService
    {
        private static IReconnectionAlgorithm _reconnect;
        private static ConnectionSettings _connectSettings;
        private static ConnectMessage _connectMessage;
        private static readonly NameValueCollection AppSettings = ConfigurationManager.AppSettings;
        private static string _rpcQueueConnectName;
        private static readonly Logger Logger = LogManager.GetLogger("General");

        private static IPublisher _connectPublisher;
        private static Consumer _connectConsumer;
        private static Consumer _pingConsumer;


        public static void Start()
        {
            _rpcQueueConnectName = AppSettings["RabbitQueueConnectName"];
            _connectSettings = new ConnectionSettings(AppSettings["RabbitHostname"], AppSettings["RabbitUsername"], AppSettings["RabbitPassword"]);
            _reconnect = new FixedIntervalReconnectionAlgorithm(3000);
            _connectMessage = new ConnectMessage
            {
                Timestamp = DateTime.UtcNow,
                ApplicationRetries = Convert.ToInt32(AppSettings["ApplicationRetries"]),
                ApplicationName = Environment.MachineName + "_" + Process.GetCurrentProcess().Id,
                ApplicationTimeout = Convert.ToInt32(AppSettings["ApplicationTimeout"]),
                GroupTimeout = Convert.ToInt32(AppSettings["GroupTimeout"]),
                GroupName = AppSettings["GroupName"],
                GroupRetries = Convert.ToInt32(AppSettings["GroupRetries"])
            };

            _connectPublisher = CreatePublisher(_rpcQueueConnectName);
            

            Logger.Log(LogLevel.Debug, "Heartbeat Client connector started");

            SendRpcConnectMessageToQueue();
        }

        public static void Stop()
        {
            _connectPublisher.Dispose();
            _connectConsumer.Dispose();
            _pingConsumer.Dispose();
        }


        private static void SendRpcConnectMessageToQueue()
        {

            string correlationId = Guid.NewGuid().ToString();

            var replyToQueueName = Guid.NewGuid().ToString();

            var publishMessage = new PublishMessage<ConnectMessage>(_connectMessage, _rpcQueueConnectName, false, "", correlationId, replyToQueueName);


            //Declare Consumer
            var replyToQueue = new Queue(replyToQueueName, false, false, true, string.Empty, null, null);
            var connectConsumerSettings = new ConsumerSettings(_connectSettings,
                _reconnect, replyToQueue, null, 0, 1, 1);
            _connectConsumer = new Consumer(connectConsumerSettings);
            _connectConsumer.Start(HandleReceivedConnectMessage);


            //Publish connect message
            _connectPublisher.PublishMessage(publishMessage);

 
        }

        private static Task<IMessage> HandleReceivedPingMessage(IMessage data)
        {

            var connectQueue = new Queue(data.ReplyTo, false, false, true, string.Empty, null, null);

            var exchange = new Exchange("", Igc.RabbitMq.ExchangeType.Direct) {Queues = new List<Queue> {connectQueue}};

            //var publisherSettings = new PublisherSettings(_connectSettings, _reconnect, exchange, false, SerializationType.None, false);
            //var publisher = new Publisher(publisherSettings);
            //publisher.Start();


            var publishMessage = new PublishMessage<string>("Pong", data.ReplyTo, false, "", data.CorrelationId,
                data.ReplyTo);
            _connectPublisher.Publish(publishMessage, "");

           Logger.Log(LogLevel.Debug,"Sending pong for: {0} with correlation ID: {1}", data.ReplyTo, data.CorrelationId);

            return Task.FromResult(data);
        }

        private static Task<IMessage> HandleReceivedConnectMessage(IMessage data)
        {
           Logger.Log(LogLevel.Debug,"Received connect {0}", data.Payload);

            var tempQueue = new Queue(data.Payload, false, false, true, string.Empty, null, null);

            var pingConsumerSettings = new ConsumerSettings(_connectSettings,_reconnect, tempQueue, null, 0, 1, 1);
            _pingConsumer = new Consumer(pingConsumerSettings);
            _pingConsumer.Start(HandleReceivedPingMessage);

            return Task.FromResult(data);
        }

        private static IPublisher CreatePublisher(string queuename)
        {
            var connectQueue = new Queue(queuename);

            var exchange = new Exchange("", Igc.RabbitMq.ExchangeType.Fanout);
            exchange.Queues = new List<Queue> { connectQueue };


            //Declare Publisher
            var publisherSettings = new PublisherSettings(_connectSettings, _reconnect, exchange, false, SerializationType.Json, false);
            var publisher = new Publisher(publisherSettings);
            publisher.Start();

            return publisher;
        }
    }
}
