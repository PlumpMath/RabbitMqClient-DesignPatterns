using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Igc.RabbitMq;
using Igc.RabbitMq.Consumption;
using Igc.RabbitMq.Serialization;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using ExchangeType = RabbitMQ.Client.ExchangeType;

namespace RpcSender
{
    class SampleClientService
    {
        private string _hostName = "localhost";
        private string _userName = "guest";
        private string _password = "guest";

        private readonly IReconnectionAlgorithm Reconnect;

        private readonly ConnectionSettings ConnectSettings;


        private string _rpcQueueConnectName = "RpcQueueConnect";

        
        public SampleClientService()
        {
            ConnectSettings = new ConnectionSettings(_hostName, _userName, _password);
            Reconnect = new FixedIntervalReconnectionAlgorithm(3000);

        }


        public void SendRpcConnectMessageToQueue(string message)
        {
           

            string correlationId = Guid.NewGuid().ToString();

            var replyToQueueName = Guid.NewGuid().ToString();

            var connectQueue = new Queue(_rpcQueueConnectName);

            var exchange = new Exchange("", Igc.RabbitMq.ExchangeType.Fanout);
            exchange.Queues = new List<Queue> { connectQueue };



            var publisherSettings = new PublisherSettings(ConnectSettings,Reconnect, exchange, false, SerializationType.None , false);
            var publisher = new Publisher(publisherSettings);
            publisher.Start();

            var publishMessage = new PublishMessage<string>(message, _rpcQueueConnectName, false,"", correlationId, replyToQueueName );
            publisher.PublishMessage(publishMessage);

            var replyToQueue = new Queue(replyToQueueName, false, false, true, string.Empty, null, null);

            var connectConsumerSettings = new ConsumerSettings(ConnectSettings,
                Reconnect, replyToQueue, null, 0, 1, 1);
            var connectConsumer = new Consumer(connectConsumerSettings);
            connectConsumer.Start(HandleReceivedConnectMessage);
        }

        private Task<IMessage> HandleReceivedPingMessage(IMessage data)
        {

            var connectQueue = new Queue(data.ReplyTo,false, false, true, string.Empty, null, null);

            var exchange = new Exchange("", Igc.RabbitMq.ExchangeType.Direct);
            exchange.Queues = new List<Queue> { connectQueue };

            var publisherSettings = new PublisherSettings(ConnectSettings, Reconnect, exchange, false,SerializationType.None, false);
            var publisher = new Publisher(publisherSettings);
            publisher.Start();


            var publishMessage = new PublishMessage<string>("Pong", data.ReplyTo, false, "", data.CorrelationId,
                data.ReplyTo);
            publisher.PublishMessage(publishMessage);

            Console.WriteLine("Sending pong for: {0} with correlation ID: {1}", data.ReplyTo,data.CorrelationId );

            return Task.FromResult(data);
        }

        private Task<IMessage> HandleReceivedConnectMessage(IMessage data)
        {
            Console.WriteLine("Revived connect {0}", data.Payload);

            var tempQueue = new Queue(data.Payload, false, false, true, string.Empty, null, null);

            var pingConsumerSettings = new ConsumerSettings(ConnectSettings,
                Reconnect, tempQueue, null, 0, 1, 1);
            var pingConsumer = new Consumer(pingConsumerSettings);
            pingConsumer.Start(HandleReceivedPingMessage);


            return Task.FromResult(data);
        }
    }
}
