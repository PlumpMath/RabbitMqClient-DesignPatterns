using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Configuration;
using System.Diagnostics;
using System.Threading.Tasks;
using Heartbeat.Contracts;
using Igc.RabbitMq;
using Igc.RabbitMq.Consumption;

namespace RpcSender
{
    class SampleClientService
    {
        private readonly IReconnectionAlgorithm _reconnect;
        private readonly ConnectionSettings _connectSettings;
        private readonly ConnectMessage _connectMessage;
        private readonly NameValueCollection _appSettings = ConfigurationManager.AppSettings;
        private readonly string _rpcQueueConnectName;

        public SampleClientService()
        {
            _rpcQueueConnectName = _appSettings["RabbitQueueConnectName"];
            _connectSettings = new ConnectionSettings(_appSettings["RabbitHostname"], _appSettings["RabbitUsername"], _appSettings["RabbitPassword"]);
            _reconnect = new FixedIntervalReconnectionAlgorithm(3000);
            _connectMessage = new ConnectMessage
            {
                Timestamp = DateTime.UtcNow,
                ApplicationRetries = Convert.ToInt32(_appSettings["ApplicationRetries"]),
                ApplicationName = Environment.MachineName + "_" + Process.GetCurrentProcess().Id,
                ApplicationTimeout = Convert.ToInt32(_appSettings["ApplicationTimeout"]),
                GroupTimeout = Convert.ToInt32(_appSettings["GroupTimeout"]),
                GroupName = _appSettings["GroupName"],
                GroupRetries = Convert.ToInt32(_appSettings["GroupRetries"])
            };

        }


        public void SendRpcConnectMessageToQueue()
        {


            string correlationId = Guid.NewGuid().ToString();

            var replyToQueueName = Guid.NewGuid().ToString();

            var connectQueue = new Queue(_rpcQueueConnectName);

            var exchange = new Exchange("", Igc.RabbitMq.ExchangeType.Fanout);
            exchange.Queues = new List<Queue> { connectQueue };


            //Declare Publisher
            var publisherSettings = new PublisherSettings(_connectSettings, _reconnect, exchange, false, SerializationType.Json, false);
            var publisher = new Publisher(publisherSettings);
            publisher.Start();

            var publishMessage = new PublishMessage<ConnectMessage>(_connectMessage, _rpcQueueConnectName, false, "", correlationId, replyToQueueName);


            //Declare Consumer
            var replyToQueue = new Queue(replyToQueueName, false, false, true, string.Empty, null, null);
            var connectConsumerSettings = new ConsumerSettings(_connectSettings,
                _reconnect, replyToQueue, null, 0, 1, 1);
            var connectConsumer = new Consumer(connectConsumerSettings);
            connectConsumer.Start(HandleReceivedConnectMessage);


            //Publish connect message
            publisher.PublishMessage(publishMessage);

 
        }

        private Task<IMessage> HandleReceivedPingMessage(IMessage data)
        {

            var connectQueue = new Queue(data.ReplyTo, false, false, true, string.Empty, null, null);

            var exchange = new Exchange("", Igc.RabbitMq.ExchangeType.Direct);
            exchange.Queues = new List<Queue> { connectQueue };

            var publisherSettings = new PublisherSettings(_connectSettings, _reconnect, exchange, false, SerializationType.None, false);
            var publisher = new Publisher(publisherSettings);
            publisher.Start();


            var publishMessage = new PublishMessage<string>("Pong", data.ReplyTo, false, "", data.CorrelationId,
                data.ReplyTo);
            publisher.PublishMessage(publishMessage);

            Console.WriteLine("Sending pong for: {0} with correlation ID: {1}", data.ReplyTo, data.CorrelationId);

            return Task.FromResult(data);
        }

        private Task<IMessage> HandleReceivedConnectMessage(IMessage data)
        {
            Console.WriteLine("Received connect {0}", data.Payload);

            var tempQueue = new Queue(data.Payload, false, false, true, string.Empty, null, null);

            var pingConsumerSettings = new ConsumerSettings(_connectSettings,
                _reconnect, tempQueue, null, 0, 1, 1);
            var pingConsumer = new Consumer(pingConsumerSettings);
            pingConsumer.Start(HandleReceivedPingMessage);


            return Task.FromResult(data);
        }
    }
}
