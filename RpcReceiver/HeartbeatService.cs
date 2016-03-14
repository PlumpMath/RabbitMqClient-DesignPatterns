using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Timers;
using Igc.RabbitMq;
using Igc.RabbitMq.Consumption;
using Igc.RabbitMq.Serialization;
using ExchangeType = Igc.RabbitMq.ExchangeType;

namespace RpcReceiver
{
    class HeartbeatService
    {
        //private string _rpcQueuePingName = "RpcQueuePing";

        private static readonly ConcurrentDictionary<string, SubscriberData> _clientPropertiesSubscriptions = new ConcurrentDictionary<string, SubscriberData>();

        private static IConsumerSettings consumerSettings;

        public HeartbeatService()
        {

            var heartbeatLoader = new XmlConsumerSettingsLoader("Consumer.xml");
            consumerSettings = heartbeatLoader.Load();
            var heartbeatConsumer = new Consumer(consumerSettings);
            heartbeatConsumer.Start(HandleConnectMessage);



            Timer checkForTime = new Timer(10 * 1000);
            checkForTime.Elapsed += checkForTime_Elapsed;
            checkForTime.Enabled = true;

        }

        void checkForTime_Elapsed(object sender, ElapsedEventArgs e)
        {
            foreach (var subs in _clientPropertiesSubscriptions.Keys)
            {
                SendPingMessageToQueue(_clientPropertiesSubscriptions[subs]);
            }
        }

        private void CreateConsumer(IPublisher publisher, string guid, string publishQueue)
        {
            var listenToQueue = "listen_" + guid;

            var queue = new Queue(listenToQueue, false, false, true, string.Empty, null, null);
            var thisConsumerSettings = new ConsumerSettings(consumerSettings.ConnectionSettings,
                consumerSettings.ReconnectionAlgorithm, queue, null, 0, 1, 1);
            var consumer = new Consumer(thisConsumerSettings);
            consumer.Start(HandleHeartbeatMessage);

            Timer timer = new Timer(5000);
            timer.Elapsed += (sender, e) => Timer_Elapsed(sender, e, listenToQueue);
            var subData = new SubscriberData {ReplyTo = listenToQueue, Timer = timer, Publisher = publisher, PublishQueue = publishQueue};
            _clientPropertiesSubscriptions.TryAdd(listenToQueue, subData);

        }

        private void SendPingMessageToQueue(SubscriberData subscriber)
        {
            string correlationId = Guid.NewGuid().ToString();

            Console.WriteLine("Sending ping for + " + subscriber.PublishQueue + "with CorrelationID : " + correlationId);

            subscriber.CorrelationId = correlationId;
            subscriber.Timer.Enabled = true;

            var publisher = subscriber.Publisher;

            var publishMessage = new PublishMessage<string>("Ping", subscriber.PublishQueue, false, "", correlationId,subscriber.ReplyTo);

            publisher.PublishMessage(publishMessage);
        }

        //private static void Consumer_Received(object sender, BasicDeliverEventArgs e)
        //{
        //    if (!_clientPropertiesSubscriptions.ContainsKey(e.RoutingKey))
        //    {
        //        Console.Write("Not Found: " + e.RoutingKey);
        //        return;
        //    }

        //    var subscriber = _clientPropertiesSubscriptions[e.RoutingKey];

        //    if (e.BasicProperties == null || e.BasicProperties.CorrelationId != subscriber.CorrelationId) return;

        //    subscriber.Timer.Enabled = false;
        //    string response = Encoding.UTF8.GetString(e.Body);
        //    Console.WriteLine("Received " + response + " for : " + subscriber.CorrelationId + " with queue : " + subscriber.BasicProperties.ReplyTo);
        //}

        private static void Timer_Elapsed(object sender, ElapsedEventArgs e, string queueName)
        {
            SubscriberData diedSub;
            Console.WriteLine("Removed queue : " + queueName);
            if (_clientPropertiesSubscriptions.TryRemove(queueName, out diedSub))
            {
                diedSub.Timer.Stop();
                diedSub.Timer.Close();
                Console.WriteLine("Consumer died: " + diedSub.ReplyTo);
            }

            else
            {
                Console.WriteLine("Could not be removed from queue");
            }

            //throw new NotImplementedException();
        }

        private Task<IMessage> HandleHeartbeatMessage(IMessage data)
        {
            string routingKey = data.RoutingKey;
            if (!_clientPropertiesSubscriptions.ContainsKey(routingKey))
            {
                Console.Write("Not Found: " + routingKey);
                return Task.FromResult<IMessage>(null);
            }

            var subscriber = _clientPropertiesSubscriptions[routingKey];

            if (data.CorrelationId != subscriber.CorrelationId)
                return Task.FromResult<IMessage>(null);

            subscriber.Timer.Enabled = false;
            Console.WriteLine("Received {0} for : {1} with queue : {2}", data.Payload, subscriber.CorrelationId, data.RoutingKey);

            return Task.FromResult(data);
        }

        private Task<IMessage> HandleConnectMessage(IMessage data)
        {
            string message = data.Payload;
            Console.WriteLine("Received message: " + message);

            var guid = Guid.NewGuid();
            var publishQueue = "publish_" + guid;



            var queue = new Queue(data.ReplyTo, false, false, true, string.Empty, null, null);

            var exchange = new Exchange("", ExchangeType.Direct);
            exchange.Queues = new List<Queue> { queue };

            var publisherSettings = new PublisherSettings(consumerSettings.ConnectionSettings,consumerSettings.ReconnectionAlgorithm, exchange, false,SerializationType.None, false);
            var publisher = new Publisher(publisherSettings);
            publisher.Start();

            var publishMessage = new PublishMessage<string>(publishQueue, data.ReplyTo, false, "", data.CorrelationId, data.ReplyTo);
            publisher.PublishMessage(publishMessage);

            this.CreateConsumer(publisher, guid.ToString(), publishQueue);

            return Task.FromResult(data);
        }
    }

    class SubscriberData
    {
        public string ReplyTo { get; set; }

        public Timer Timer { get; set; }

        public string PublishQueue { get; set; }

        public string CorrelationId { get; set; }

        public IPublisher Publisher { get; set; }

    }
}
