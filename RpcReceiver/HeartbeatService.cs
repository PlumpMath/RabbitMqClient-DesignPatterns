﻿using System;
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
using NLog;
using ExchangeType = Igc.RabbitMq.ExchangeType;

namespace RpcReceiver
{
    public class HeartbeatService
    {
        public static ConcurrentDictionary<string, ApplicationInfoDto> ApplicationSubscriptions;
        private static IConsumerSettings _consumerSettings;
        private readonly NameValueCollection _appSettings = ConfigurationManager.AppSettings;

        public static ConcurrentDictionary<string, HashSet<String>> _groupApps;
        private static ConcurrentDictionary<string, Publisher> _pingPublishers;
        private static ConcurrentDictionary<string, Timer> _pingTimers;

        private static Logger _logger = LogManager.GetLogger("General");

        public HeartbeatService()
        {
            _groupApps = BinarySerialization.ReadFromBinaryFile<ConcurrentDictionary<string, HashSet<string>>>(_appSettings["GroupPath"]) ??
                         new ConcurrentDictionary<string, HashSet<string>>();
            ApplicationSubscriptions =
                BinarySerialization.ReadFromBinaryFile<ConcurrentDictionary<string, ApplicationInfoDto>>(_appSettings["AppPath"]) ?? new ConcurrentDictionary<string, ApplicationInfoDto>();

            _pingPublishers = new ConcurrentDictionary<string, Publisher>();
            _pingTimers = new ConcurrentDictionary<string, Timer>();

            foreach(var listenQueue in ApplicationSubscriptions.Keys)
            {
                CreateTimer(ApplicationSubscriptions[listenQueue].ApplicationTimeout,listenQueue);
            }

            var heartbeatLoader = new XmlConsumerSettingsLoader("Consumer.xml");
            _consumerSettings = heartbeatLoader.Load();
            var heartbeatConsumer = new Consumer(_consumerSettings);
            heartbeatConsumer.Start(HandleConnectMessage);



            Timer pingTimer = new Timer(Convert.ToInt32(_appSettings["PingFrequency"]) * 1000);
            pingTimer.Elapsed += PingClientsEventHandler;
            pingTimer.Enabled = true;

            _logger.Log(LogLevel.Info,"Heartbeat Service Started");

        }

        private void PingClientsEventHandler(object sender, ElapsedEventArgs e)
        {
            foreach (var subs in ApplicationSubscriptions.Keys)
            {
                var currentSub = ApplicationSubscriptions[subs];

                var secondsDiffereance = (DateTime.Now - currentSub.LastUpdated).TotalSeconds;

                // check if the subscriber has been dead for a long time and delete it 
                if (secondsDiffereance > Convert.ToInt32(_appSettings["ApplicationDisconnectTimeout"]))
                {
                    ApplicationInfoDto dummy;
                    ApplicationSubscriptions.TryRemove(subs, out dummy);
                    PersistApp();

                    Timer x = _pingTimers[dummy.ListenToQueue];
                    x.Stop();
                    x.Dispose();
                    _pingTimers.TryRemove(dummy.ListenToQueue, out x);

                    if (_groupApps.ContainsKey(currentSub.GroupName))
                    {
                        _groupApps[currentSub.GroupName].Remove(currentSub.ApplicationName);
                    }
                   _logger.Log(LogLevel.Info,"Removed Application:  {0} which was dead for a long time",
                        dummy.ApplicationName);
                }
                else
                {
                    SendPingMessageToQueue(currentSub);
                }

            }
        }

        private void CreateConsumer(string guid, string publishQueue,
            ConnectMessage connectMessage, string replyToQueue)
        {
            var listenToQueue = "listen_" + guid;

            var queue = new Queue(listenToQueue, false, false, true, string.Empty, null, null);
            var thisConsumerSettings = new ConsumerSettings(_consumerSettings.ConnectionSettings,
                _consumerSettings.ReconnectionAlgorithm, queue, null, 0, 1, 1);
            var consumer = new Consumer(thisConsumerSettings);
            consumer.Start(HandleHeartbeatMessage);

            CreateTimer(connectMessage.ApplicationTimeout, listenToQueue);

            var subData = new ApplicationInfoDto
            {
                CurrentApplicationRetries = connectMessage.ApplicationRetries,
                CurrentGroupRetries = connectMessage.GroupRetries,
                ListenToQueue = listenToQueue,
                PublishQueue = publishQueue,
                LastUpdated = DateTime.Now,
                ApplicationName = connectMessage.ApplicationName,
                ApplicationRetries = connectMessage.ApplicationRetries,
                ApplicationTimeout = connectMessage.ApplicationTimeout,
                GroupName = connectMessage.GroupName,
                GroupRetries = connectMessage.GroupRetries,
                GroupTimeout = connectMessage.GroupTimeout

            };
            ApplicationSubscriptions.TryAdd(listenToQueue, subData);
            PersistApp();

            //Register the client to the Group
            if (_groupApps.ContainsKey(connectMessage.GroupName))
            {
                //todo: if there is no item in the group send alert that the Group is up else send alert that a new consumer is added.
                if (_groupApps[connectMessage.GroupName].Count == 0)
                {
                    _logger.Log(LogLevel.Info,"Group has revived {0} and application {1} connected", connectMessage.GroupName,
                        connectMessage.ApplicationName);
                }
                else
                {
                   _logger.Log(LogLevel.Info,"New Application {0} added to Group {1}", connectMessage.ApplicationName,
                        connectMessage.GroupName);
                }
                _groupApps[connectMessage.GroupName].Add(connectMessage.ApplicationName);
            }
            else
            {
                //todo: Add alert that a new group was created
               _logger.Log(LogLevel.Info,"New Group Created: {0} and application {1} connected", connectMessage.GroupName,
                    connectMessage.ApplicationName);
                _groupApps.TryAdd(connectMessage.GroupName, new HashSet<string> { connectMessage.ApplicationName });
                PersistGroup();
            }

        }

        private void SendPingMessageToQueue(ApplicationInfoDto subscriber)
        {
            string correlationId = Guid.NewGuid().ToString();

           _logger.Log(LogLevel.Info,"Sending ping for + " + subscriber.PublishQueue + "with CorrelationID : " + correlationId);

            subscriber.CorrelationId = correlationId;
            _pingTimers[subscriber.ListenToQueue].Enabled = true;

            var publisher = GetPublisher(subscriber.ListenToQueue);

            var publishMessage = new PublishMessage<string>("Ping", subscriber.PublishQueue, false, "", correlationId,
                subscriber.ListenToQueue);

            publisher.PublishMessage(publishMessage);
        }


        private static void ApplicationTimeoutEventHandler(object sender, ElapsedEventArgs e, string queueName)
        {
            if (ApplicationSubscriptions.ContainsKey(queueName))
            {
                var currentSub = ApplicationSubscriptions[queueName];
                _pingTimers[currentSub.ListenToQueue].Enabled = false;

                if (currentSub.CurrentApplicationRetries == 0)
                {
                    //todo: add logic to remove sending ping for apps that has been running for a long time. 
                   _logger.Log(LogLevel.Info,"Application {0} is still dead", currentSub.ApplicationName);

                    ProcessGroup(currentSub);
                }

                else if (currentSub.CurrentApplicationRetries == 1)
                {

                    //todo: Throw alert that consumer died
                   _logger.Log(LogLevel.Info,"Consumer died: " + currentSub.ApplicationName);
                    currentSub.CurrentApplicationRetries--;
                    ProcessGroup(currentSub);


                }
                else
                {
                    currentSub.CurrentApplicationRetries--;
                   _logger.Log(LogLevel.Info,"Reducing Retries for queue: {0}, Remaining retriers are : {1}", queueName,
                        currentSub.CurrentApplicationRetries);
                }
            }

            else
            {
               _logger.Log(LogLevel.Info,"Queue {0} already deleted", queueName);
            }

        }

        private static void ProcessGroup(ApplicationInfoDto currentSub)
        {

            ApplicationInfoDto dummy;

            if (!_groupApps.ContainsKey(currentSub.GroupName))
            {
               _logger.Log(LogLevel.Info,"Group Died ");
                //todo: Throw no consumer EventHandler 
            }
            else
            {
                _groupApps[currentSub.GroupName].Remove(currentSub.ApplicationName);

                if (_groupApps[currentSub.GroupName].Count == 0 && currentSub.CurrentGroupRetries == 1)
                {
                   _logger.Log(LogLevel.Info,"Group Died ");

                    foreach (var subs in ApplicationSubscriptions.Keys.Where(
                        subs =>
                            ApplicationSubscriptions[subs].GroupName ==
                            currentSub.GroupName))
                    {
                        ApplicationSubscriptions.TryRemove(subs, out dummy);

                        Timer x = _pingTimers[dummy.ListenToQueue];
                        x.Stop();
                        x.Dispose();
                        _pingTimers.TryRemove(dummy.ListenToQueue,out x);
                    }

                    //todo: Throw no consumer EventHandler 
                }
                else if (_groupApps[currentSub.GroupName].Count == 0)
                {
                    currentSub.CurrentGroupRetries--;
                   _logger.Log(LogLevel.Info,"Reducing the number of group retires remaining is : {0}",
                        currentSub.CurrentGroupRetries);
                }
            }
        }

        private Task<IMessage> HandleHeartbeatMessage(IMessage data)
        {
            string routingKey = data.RoutingKey;
            if (!ApplicationSubscriptions.ContainsKey(routingKey))
            {
                Console.Write("Not Found: " + routingKey);
                return Task.FromResult<IMessage>(null);
            }

            var subscriber = ApplicationSubscriptions[routingKey];

            if (data.CorrelationId != subscriber.CorrelationId)
                return Task.FromResult<IMessage>(null);

            _pingTimers[subscriber.ListenToQueue].Enabled = false;
            //reset the retries
            subscriber.CurrentApplicationRetries = subscriber.ApplicationRetries;
            subscriber.CurrentGroupRetries = subscriber.GroupRetries;
            subscriber.LastUpdated = DateTime.Now;
            //check if the item has died before and add it to the group
            if (!_groupApps.ContainsKey(subscriber.GroupName))
            {
                //todo: add alert that application has reconnected
               _logger.Log(LogLevel.Info,"Application{0} reconnected for group{1}: ", subscriber.ApplicationName,
                    subscriber.GroupName);
                _groupApps[subscriber.GroupName].Add(subscriber.ApplicationName);
            }

           _logger.Log(LogLevel.Info,"Received {0} for : {1} with queue : {2}", data.Payload, subscriber.CorrelationId,
                data.RoutingKey);

            return Task.FromResult(data);
        }

        private Task<IMessage> HandleConnectMessage(IMessage data)
        {
            var connectMessage = JsonSerialisation.DeserializeFromString<ConnectMessage>(data.Payload, true);
           _logger.Log(LogLevel.Info,"Received message for Application:{0}", connectMessage.ApplicationName);

            var guid = Guid.NewGuid();
            var publishQueue = "publish_" + guid;

            var publisher = GetPublisher(data.ReplyTo);
           
           _logger.Log(LogLevel.Info,"Publishing to queue {0} data {1}", data.ReplyTo, publishQueue);
            CreateConsumer(guid.ToString(), publishQueue, connectMessage, data.ReplyTo);

            var publishMessage = new PublishMessage<string>(publishQueue, data.ReplyTo, false, "", data.CorrelationId,
                data.ReplyTo);
            publisher.PublishMessage(publishMessage);

            return Task.FromResult(data);
        }

        private Publisher GetPublisher(string queueName)
        {
            if (_pingPublishers.ContainsKey(queueName))
                return _pingPublishers[queueName];

            var queue = new Queue(queueName, false, false, true, string.Empty, null, null);

            var exchange = new Exchange("", ExchangeType.Direct) {Queues = new List<Queue> {queue}};

            var publisherSettings = new PublisherSettings(_consumerSettings.ConnectionSettings,
                _consumerSettings.ReconnectionAlgorithm, exchange, false, SerializationType.None, false);

            var publisher = new Publisher(publisherSettings);
            publisher.Start();

            _pingPublishers.TryAdd(queueName, publisher);

            return publisher;
        }

        private void CreateTimer(int timeout, string listenToQueue)
        {
            if (_pingTimers.ContainsKey(listenToQueue)) return;

            Timer applicationTimoutTimer = new Timer(timeout * 1000);
            applicationTimoutTimer.Elapsed += (sender, e) => ApplicationTimeoutEventHandler(sender, e, listenToQueue);

            _pingTimers.TryAdd(listenToQueue, applicationTimoutTimer);
        }

        private void PersistGroup()
        {
            BinarySerialization.WriteToBinaryFile(_appSettings["GroupPath"], _groupApps);
        }

        private void PersistApp()
        {
            BinarySerialization.WriteToBinaryFile(_appSettings["AppPath"], ApplicationSubscriptions);
        }
    }
}
