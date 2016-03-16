using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Configuration;
using System.Linq;
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
        private static readonly ConcurrentDictionary<string, ApplicationInfoDto> ClientPropertiesSubscriptions =
            new ConcurrentDictionary<string, ApplicationInfoDto>();

        private static IConsumerSettings _consumerSettings;
        private readonly NameValueCollection _appSettings = ConfigurationManager.AppSettings;

        private static ConcurrentDictionary<string, HashSet<String>> _groupApps =
            new ConcurrentDictionary<string, HashSet<string>>();


        public HeartbeatService()
        {
            _groupApps = new ConcurrentDictionary<string, HashSet<string>>();
            var heartbeatLoader = new XmlConsumerSettingsLoader("Consumer.xml");
            _consumerSettings = heartbeatLoader.Load();
            var heartbeatConsumer = new Consumer(_consumerSettings);
            heartbeatConsumer.Start(HandleConnectMessage);



            Timer pingTimer = new Timer(Convert.ToInt32(_appSettings["PingFrequency"])*1000);
            pingTimer.Elapsed += PingClientsEventHandler;
            pingTimer.Enabled = true;

        }

        private void PingClientsEventHandler(object sender, ElapsedEventArgs e)
        {
            foreach (var subs in ClientPropertiesSubscriptions.Keys)
            {
                var currentSub = ClientPropertiesSubscriptions[subs];

                var secondsDiffereance = (DateTime.Now - currentSub.LastUpdated).TotalSeconds;

                // check if the subscriber has been dead for a long time and delete it 
                if (secondsDiffereance > Convert.ToInt32(_appSettings["ApplicationDisconnectTimeout"]))
                {
                    ApplicationInfoDto dummy;
                    ClientPropertiesSubscriptions.TryRemove(subs, out dummy);
                    dummy.Timer.Stop();
                    dummy.Timer.Dispose();

                    if (_groupApps.ContainsKey(currentSub.GroupName))
                    {
                        _groupApps[currentSub.GroupName].Remove(currentSub.ApplicationName);
                    }
                    Console.WriteLine("Removed Application:  {0} which was dead for a long time",
                        dummy.ApplicationName);
                }
                else
                {
                    SendPingMessageToQueue(currentSub);
                }

            }
        }

        private void CreateConsumer(IPublisher publisher, string guid, string publishQueue,
            ConnectMessage connectMessage)
        {
            var listenToQueue = "listen_" + guid;

            var queue = new Queue(listenToQueue, false, false, true, string.Empty, null, null);
            var thisConsumerSettings = new ConsumerSettings(_consumerSettings.ConnectionSettings,
                _consumerSettings.ReconnectionAlgorithm, queue, null, 0, 1, 1);
            var consumer = new Consumer(thisConsumerSettings);
            consumer.Start(HandleHeartbeatMessage);

            Timer applicationTimoutTimer = new Timer(connectMessage.ApplicationTimeout*1000);
            applicationTimoutTimer.Elapsed += (sender, e) => ApplicationTimeoutEventHandler(sender, e, listenToQueue);
            var subData = new ApplicationInfoDto
            {
                CurrentApplicationRetries = connectMessage.ApplicationRetries,
                CurrentGroupRetries = connectMessage.GroupRetries,
                ReplyTo = listenToQueue,
                Timer = applicationTimoutTimer,
                Publisher = publisher,
                PublishQueue = publishQueue,
                LastUpdated = DateTime.Now,
                ApplicationName = connectMessage.ApplicationName,
                ApplicationRetries = connectMessage.ApplicationRetries,
                ApplicationTimeout = connectMessage.ApplicationTimeout,
                GroupName = connectMessage.GroupName,
                GroupRetries = connectMessage.GroupRetries,
                GroupTimeout = connectMessage.GroupTimeout

            };
            ClientPropertiesSubscriptions.TryAdd(listenToQueue, subData);

            //Register the client to the Group
            if (_groupApps.ContainsKey(connectMessage.GroupName))
            {
                //todo: if there is no item in the group send alert that the Group is up else send alert that a new consumer is added.
                if (_groupApps[connectMessage.GroupName].Count == 0)
                {
                    Console.WriteLine("Group has revived {0} and application {1} connected", connectMessage.GroupName,
                        connectMessage.ApplicationName);
                }
                else
                {
                    Console.WriteLine("New Application {0} added to Group {1}", connectMessage.ApplicationName,
                        connectMessage.GroupName);
                }
                _groupApps[connectMessage.GroupName].Add(connectMessage.ApplicationName);
            }
            else
            {
                //todo: Add alert that a new group was created
                Console.WriteLine("New Group Created: {0} and application {1} connected", connectMessage.GroupName,
                    connectMessage.ApplicationName);
                _groupApps.TryAdd(connectMessage.GroupName, new HashSet<string> {connectMessage.ApplicationName});
            }

        }

        private void SendPingMessageToQueue(ApplicationInfoDto subscriber)
        {
            string correlationId = Guid.NewGuid().ToString();

            Console.WriteLine("Sending ping for + " + subscriber.PublishQueue + "with CorrelationID : " + correlationId);

            subscriber.CorrelationId = correlationId;
            subscriber.Timer.Enabled = true;

            var publisher = subscriber.Publisher;

            var publishMessage = new PublishMessage<string>("Ping", subscriber.PublishQueue, false, "", correlationId,
                subscriber.ReplyTo);

            publisher.PublishMessage(publishMessage);
        }


        private static void ApplicationTimeoutEventHandler(object sender, ElapsedEventArgs e, string queueName)
        {
            if (ClientPropertiesSubscriptions.ContainsKey(queueName))
            {
                var currentSub = ClientPropertiesSubscriptions[queueName];
                currentSub.Timer.Enabled = false;

                if (currentSub.CurrentApplicationRetries == 0)
                {
                    //todo: add logic to remove sending ping for apps that has been running for a long time. 
                    Console.WriteLine("Application {0} is still dead", currentSub.ApplicationName);

                    ProcessGroup(currentSub);
                }

                else if (currentSub.CurrentApplicationRetries == 1)
                {

                    //todo: Throw alert that consumer died
                    Console.WriteLine("Consumer died: " + currentSub.ApplicationName);
                    currentSub.CurrentApplicationRetries--;
                    ProcessGroup(currentSub);


                }
                else
                {
                    currentSub.CurrentApplicationRetries--;
                    Console.WriteLine("Reducing Retries for queue: {0}, Remaining retriers are : {1}", queueName,
                        currentSub.CurrentApplicationRetries);
                }
            }

            else
            {
                Console.WriteLine("Queue {0} already deleted", queueName);
            }

        }

        private static void ProcessGroup(ApplicationInfoDto currentSub)
        {

            ApplicationInfoDto dummy;

            if (!_groupApps.ContainsKey(currentSub.GroupName))
            {
                Console.WriteLine("Group Died ");
                //todo: Throw no consumer EventHandler 
            }
            else
            {
                _groupApps[currentSub.GroupName].Remove(currentSub.ApplicationName);

                if (_groupApps[currentSub.GroupName].Count == 0 && currentSub.CurrentGroupRetries == 1)
                {
                    Console.WriteLine("Group Died ");

                    foreach (var subs in ClientPropertiesSubscriptions.Keys.Where(
                        subs =>
                            ClientPropertiesSubscriptions[subs].GroupName ==
                            currentSub.GroupName))
                    {
                        ClientPropertiesSubscriptions.TryRemove(subs, out dummy);
                        dummy.Timer.Enabled = false;
                        dummy.Timer.Stop();
                        dummy.Timer.Dispose();
                    }

                    //todo: Throw no consumer EventHandler 
                }
                else if (_groupApps[currentSub.GroupName].Count == 0)
                {
                    currentSub.CurrentGroupRetries--;
                    Console.WriteLine("Reducing the number of group retires remaining is : {0}",
                        currentSub.CurrentGroupRetries);
                }
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
            subscriber.CurrentApplicationRetries = subscriber.ApplicationRetries;
            subscriber.CurrentGroupRetries = subscriber.GroupRetries;
            subscriber.LastUpdated = DateTime.Now;
            //check if the item has died before and add it to the group
            if (!_groupApps.ContainsKey(subscriber.GroupName))
            {
                //todo: add alert that application has reconnected
                Console.WriteLine("Application{0} reconnected for group{1}: ", subscriber.ApplicationName,
                    subscriber.GroupName);
                _groupApps[subscriber.GroupName].Add(subscriber.ApplicationName);
            }

            Console.WriteLine("Received {0} for : {1} with queue : {2}", data.Payload, subscriber.CorrelationId,
                data.RoutingKey);

            return Task.FromResult(data);
        }

        private Task<IMessage> HandleConnectMessage(IMessage data)
        {
            var connectMessage = JsonSerialisation.DeserializeFromString<ConnectMessage>(data.Payload, true);
            Console.WriteLine("Received message for Application:{0}", connectMessage.ApplicationName);

            var guid = Guid.NewGuid();
            var publishQueue = "publish_" + guid;

            var queue = new Queue(data.ReplyTo, false, false, true, string.Empty, null, null);

            var exchange = new Exchange("", ExchangeType.Direct);
            exchange.Queues = new List<Queue> {queue};

            var publisherSettings = new PublisherSettings(_consumerSettings.ConnectionSettings,
                _consumerSettings.ReconnectionAlgorithm, exchange, false, SerializationType.None, false);
            var publisher = new Publisher(publisherSettings);
            publisher.Start();

            Console.WriteLine("Publishing to queue {0} data {1}", data.ReplyTo, publishQueue);

            var publishMessage = new PublishMessage<string>(publishQueue, data.ReplyTo, false, "", data.CorrelationId,
                data.ReplyTo);
            publisher.PublishMessage(publishMessage);

            CreateConsumer(publisher, guid.ToString(), publishQueue, connectMessage);

            return Task.FromResult(data);
        }
    }
}
