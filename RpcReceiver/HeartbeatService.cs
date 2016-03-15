using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Configuration;
using System.Threading.Tasks;
using System.Timers;
using Heartbeat.Contracts;
using Igc.RabbitMq;
using Igc.RabbitMq.Consumption;
using Igc.RabbitMq.Serialization;
using Igc.Sports.Json;
using ExchangeType = Igc.RabbitMq.ExchangeType;

namespace RpcReceiver
{
    class HeartbeatService
    {
        private static readonly ConcurrentDictionary<string, SubscriberData> ClientPropertiesSubscriptions = new ConcurrentDictionary<string, SubscriberData>();
        private static IConsumerSettings _consumerSettings;
        private readonly NameValueCollection _appSettings = ConfigurationManager.AppSettings;
        private static ConcurrentDictionary<string, HashSet<String>> GroupApps = new ConcurrentDictionary<string, HashSet<string>>();

        public HeartbeatService()
        {
            GroupApps = new ConcurrentDictionary<string, HashSet<string>>();
            var heartbeatLoader = new XmlConsumerSettingsLoader("Consumer.xml");
            _consumerSettings = heartbeatLoader.Load();
            var heartbeatConsumer = new Consumer(_consumerSettings);
            heartbeatConsumer.Start(HandleConnectMessage);



            Timer pingTimer = new Timer(Convert.ToInt32(_appSettings["PingFrequency"]) * 1000);
            pingTimer.Elapsed += PingClientsEventHandler;
            pingTimer.Enabled = true;

        }

        private void PingClientsEventHandler(object sender, ElapsedEventArgs e)
        {
            foreach (var subs in ClientPropertiesSubscriptions.Keys)
            {
                SendPingMessageToQueue(ClientPropertiesSubscriptions[subs]);
            }
        }

        private void CreateConsumer(IPublisher publisher, string guid, string publishQueue, ConnectMessage connectMessage)
        {
            var listenToQueue = "listen_" + guid;

            var queue = new Queue(listenToQueue, false, false, true, string.Empty, null, null);
            var thisConsumerSettings = new ConsumerSettings(_consumerSettings.ConnectionSettings,
                _consumerSettings.ReconnectionAlgorithm, queue, null, 0, 1, 1);
            var consumer = new Consumer(thisConsumerSettings);
            consumer.Start(HandleHeartbeatMessage);

            Timer applicationTimoutTimer = new Timer(connectMessage.ApplicationTimeout * 1000);
            applicationTimoutTimer.Elapsed += (sender, e) => ApplicationTimeoutEventHandler(sender, e, listenToQueue);
            var subData = new SubscriberData
            {
                ConnectionData = connectMessage,
                CurrentApplicationRetries = connectMessage.ApplicationRetries,
                CurrentGroupRetries = connectMessage.GroupRetries,
                ReplyTo = listenToQueue,
                Timer = applicationTimoutTimer,
                Publisher = publisher,
                PublishQueue = publishQueue
            };
            ClientPropertiesSubscriptions.TryAdd(listenToQueue, subData);

            //Register the client to the Group
            if (GroupApps.ContainsKey(connectMessage.GroupName))
            {
                GroupApps[connectMessage.GroupName].Add(connectMessage.ApplicationName);
            }
            else
            {
                GroupApps.TryAdd(connectMessage.GroupName, new HashSet<string> { connectMessage.ApplicationName });
            }

        }

        private void SendPingMessageToQueue(SubscriberData subscriber)
        {
            string correlationId = Guid.NewGuid().ToString();

            Console.WriteLine("Sending ping for + " + subscriber.PublishQueue + "with CorrelationID : " + correlationId);

            subscriber.CorrelationId = correlationId;
            subscriber.Timer.Enabled = true;

            var publisher = subscriber.Publisher;

            var publishMessage = new PublishMessage<string>("Ping", subscriber.PublishQueue, false, "", correlationId, subscriber.ReplyTo);

            publisher.PublishMessage(publishMessage);
        }


        private static void ApplicationTimeoutEventHandler(object sender, ElapsedEventArgs e, string queueName)
        {
            if (ClientPropertiesSubscriptions.ContainsKey(queueName))
            {
                var currentSub = ClientPropertiesSubscriptions[queueName];

                if (currentSub.CurrentApplicationRetries == 1)
                {
                    Console.WriteLine("Consumer died: " + currentSub.ConnectionData.ApplicationName);

                    SubscriberData diedSub;
                    if (ClientPropertiesSubscriptions.TryRemove(queueName, out diedSub))
                    {
                        diedSub.Timer.Stop();
                        diedSub.Timer.Close();
                        Console.WriteLine("Consumer died: " + diedSub.ReplyTo);


                        if (!GroupApps.ContainsKey(currentSub.ConnectionData.GroupName))
                        {
                            Console.WriteLine("Group Died ");
                            //Throw no consumer EventHandler 
                        }
                        else
                        {
                            GroupApps[currentSub.ConnectionData.GroupName].Remove(
                                currentSub.ConnectionData.ApplicationName);

                            if (GroupApps[currentSub.ConnectionData.GroupName].Count == 0 && currentSub.CurrentGroupRetries == 1)
                            {
                                HashSet<string> dummy;
                                GroupApps.TryRemove(currentSub.ConnectionData.GroupName, out dummy);
                                Console.WriteLine("Group Died ");
                                //Throw no consumer EventHandler 
                            }
                            else if (GroupApps[currentSub.ConnectionData.GroupName].Count == 0)
                            {
                                currentSub.CurrentGroupRetries--;
                                Console.WriteLine("Reducing the number of group retires remaining is : {0}", currentSub.CurrentGroupRetries);
                            }
                        }
                    }

                    else
                    {
                        Console.WriteLine("No of retries exceded and consumer died but could not remove it from queue");
                    }
                }
                else
                {
                    currentSub.Timer.Enabled = false;
                    currentSub.CurrentApplicationRetries--;
                    Console.WriteLine("Reducing Retries for queue: {0}, Remaining retriers are : {1}", queueName, currentSub.CurrentApplicationRetries);
                } 
            }

            else
            {
                Console.WriteLine("Queue {0} already deleted", queueName);
            }

        }

        private Task<IMessage> HandleHeartbeatMessage(IMessage data)
        {
            string routingKey = data.RoutingKey;
            if (!ClientPropertiesSubscriptions.ContainsKey(routingKey))
            {
                Console.Write("Not Found: " + routingKey);
                return Task.FromResult<IMessage>(null);
            }

            var subscriber = ClientPropertiesSubscriptions[routingKey];

            if (data.CorrelationId != subscriber.CorrelationId)
                return Task.FromResult<IMessage>(null);

            subscriber.Timer.Enabled = false;
            //reset the retries
            subscriber.CurrentApplicationRetries = subscriber.ConnectionData.ApplicationRetries;
            subscriber.CurrentGroupRetries = subscriber.ConnectionData.GroupRetries;
            Console.WriteLine("Received {0} for : {1} with queue : {2}", data.Payload, subscriber.CorrelationId, data.RoutingKey);

            return Task.FromResult(data);
        }

        private Task<IMessage> HandleConnectMessage(IMessage data)
        {
            var connectMessage = JsonSerialisation.DeserializeFromString<ConnectMessage>(data.Payload, true);
            Console.WriteLine("Received message for Application:{0}" , connectMessage.ApplicationName);

            var guid = Guid.NewGuid();
            var publishQueue = "publish_" + guid;

            var queue = new Queue(data.ReplyTo, false, false, true, string.Empty, null, null);

            var exchange = new Exchange("", ExchangeType.Direct);
            exchange.Queues = new List<Queue> { queue };

            var publisherSettings = new PublisherSettings(_consumerSettings.ConnectionSettings, _consumerSettings.ReconnectionAlgorithm, exchange, false, SerializationType.None, false);
            var publisher = new Publisher(publisherSettings);
            publisher.Start();

            Console.WriteLine("Publishing to queue {0} data {1}", data.ReplyTo, publishQueue);

            var publishMessage = new PublishMessage<string>(publishQueue, data.ReplyTo, false, "", data.CorrelationId, data.ReplyTo);
            publisher.PublishMessage(publishMessage);

            CreateConsumer(publisher, guid.ToString(), publishQueue, connectMessage);

            return Task.FromResult(data);
        }
    }

    class SubscriberData
    {
        public ConnectMessage ConnectionData { get; set; }

        public int CurrentApplicationRetries { get; set; }
        public int CurrentGroupRetries { get; set; }

        public string ReplyTo { get; set; }

        public Timer Timer { get; set; }

        public string PublishQueue { get; set; }

        public string CorrelationId { get; set; }

        public IPublisher Publisher { get; set; }

    }
}
