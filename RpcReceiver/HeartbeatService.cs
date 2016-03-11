using System;
using System.Collections.Concurrent;
using System.Text;
using System.Timers;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace RpcReceiver
{
    class HeartbeatService
    {

        private string _hostName = "localhost";
        private string _userName = "guest";
        private string _password = "guest";
        private string _rpcQueueConnectName = "RpcQueueConnect";
        //private string _rpcQueuePingName = "RpcQueuePing";
        private readonly IModel model;

        private static readonly ConcurrentDictionary<string, SubscriberData> _clientPropertiesSubscriptions = new ConcurrentDictionary<string,SubscriberData>();
        
        const double interval60Minutes = 10 * 1000; // milliseconds to one minute

        public HeartbeatService()
        {

            ConnectionFactory connectionFactory = new ConnectionFactory();
            connectionFactory.HostName = _hostName;
            connectionFactory.UserName = _userName;
            connectionFactory.Password = _password;

            model = connectionFactory.CreateConnection().CreateModel();

            Timer checkForTime = new Timer(interval60Minutes);
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
        public void ReceiveRpcConnectMessage()
        {
            model.BasicQos(0, 1, false);
            QueueingBasicConsumer consumer = new QueueingBasicConsumer(model);
            model.BasicConsume(_rpcQueueConnectName, false, consumer);

            while (true)
            {
                BasicDeliverEventArgs deliveryArguments = consumer.Queue.Dequeue() as BasicDeliverEventArgs;
                string message = Encoding.UTF8.GetString(deliveryArguments.Body);

                //_clientSubscriptions.Add(message);

                Console.WriteLine("Received message: " + message);
                IBasicProperties replyBasicProperties = model.CreateBasicProperties();
                replyBasicProperties.CorrelationId = deliveryArguments.BasicProperties.CorrelationId;

                var guid = Guid.NewGuid();
                var publishQueue = "publish_" +guid ;

                model.QueueDeclare(publishQueue, false, false, true, null);
                
                

                byte[] responseBytes = Encoding.UTF8.GetBytes(publishQueue);

                model.BasicPublish("", deliveryArguments.BasicProperties.ReplyTo, replyBasicProperties, responseBytes);
                model.BasicAck(deliveryArguments.DeliveryTag, false);
                CreateConsumer(publishQueue,guid.ToString());
                
            }
        } 

        private void CreateConsumer(string publishQueue, string guid)
        {
            var listenToQueue = "listen_" + guid;
            model.QueueDeclare(listenToQueue, false, false, true, null);
            var tmpConsumer = new EventingBasicConsumer(model);
            model.BasicConsume(listenToQueue, true, tmpConsumer);

            IBasicProperties basicProperties = model.CreateBasicProperties();
            basicProperties.ReplyTo = listenToQueue;

            Timer timer = new Timer(500);
            timer.Elapsed += (sender, e) => Timer_Elapsed (sender,e,listenToQueue);
            
            _clientPropertiesSubscriptions.TryAdd(basicProperties.ReplyTo, new SubscriberData { BasicProperties = basicProperties, Consumer = tmpConsumer, Timer = timer , PublishQueue = publishQueue});
            tmpConsumer.Received += Consumer_Received;
        }

        private void SendPingMessageToQueue(SubscriberData subscriber)
        {
            string correlationId = Guid.NewGuid().ToString();
            subscriber.BasicProperties.CorrelationId = correlationId;

            Console.WriteLine("Sending ping for + " + subscriber.BasicProperties.ReplyTo + "with CorrelationID : " + correlationId);

            subscriber.CorrelationId = correlationId;
            //pingMessages[subscriber.BasicProperties.ReplyTo] = correlationId;
            subscriber.Timer.Enabled = true;
            model.BasicPublish("", subscriber.PublishQueue, subscriber.BasicProperties, Encoding.UTF8.GetBytes("Ping"));
        }

        private static void Consumer_Received(object sender, BasicDeliverEventArgs e)
        {
            if (!_clientPropertiesSubscriptions.ContainsKey(e.RoutingKey))
            {
                Console.Write("Not Found: " + e.RoutingKey);
                return;
            }
            
            var subscriber = _clientPropertiesSubscriptions[e.RoutingKey];

            if (e.BasicProperties == null || e.BasicProperties.CorrelationId != subscriber.CorrelationId) return;

            subscriber.Timer.Enabled = false;
            string response = Encoding.UTF8.GetString(e.Body);
            Console.WriteLine("Received " + response + " for : " + subscriber.CorrelationId + " with queue : " +subscriber.BasicProperties.ReplyTo);
        }

        private static void Timer_Elapsed(object sender, ElapsedEventArgs e, string queueName)
        {
            SubscriberData diedSub;
            Console.WriteLine("Removed queue : " + queueName);
            if (_clientPropertiesSubscriptions.TryRemove(queueName, out diedSub))
            {
                diedSub.Timer.Stop();
                diedSub.Timer.Close();
                Console.WriteLine("Consumer died: " + diedSub.BasicProperties.ReplyTo);
            }

            else
            {
                Console.WriteLine("Could not be removed from queue");
            }
            
            //throw new NotImplementedException();
        }
    }

    class SubscriberData
    {
        public IBasicProperties BasicProperties { get; set; }
        public EventingBasicConsumer Consumer { get; set; }
        public Timer Timer { get; set; }

        public string PublishQueue { get; set; }

        public string CorrelationId { get; set; }

    }
}
