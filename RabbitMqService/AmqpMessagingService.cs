using System;
using System.Collections.Generic;
using System.Text;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using RabbitMQ.Client.MessagePatterns;
using System.Timers;

namespace RabbitMqService
{
    public class AmqpMessagingService
    {
        private string _hostName = "localhost";
        private string _userName = "guest";
        private string _password = "guest";
        private string _exchangeName = "";
        private bool _durable = true;
        private string _rpcQueueConnectName = "RpcQueueConnect";
        private string _rpcExchangeConnectName = "RpcExchangeConnect";
        private QueueingBasicConsumer _rpcConsumer;
        private string _responseQueue;
        private string _rpcQueuePingName = "RpcQueuePing";
        private string _rpcExchangePingName = "RpcExchangePing";

        private readonly List<String> _clientSubscriptions = new List<string>();

        private readonly Dictionary<IBasicProperties, IModel> _clientPropertiesSubscriptions = new Dictionary<IBasicProperties, IModel>();

        const double interval60Minutes =   60 * 1000; // milliseconds to one minute

        public AmqpMessagingService()
        {
            Timer checkForTime = new Timer(interval60Minutes);
            checkForTime.Elapsed += new ElapsedEventHandler(checkForTime_Elapsed);
            checkForTime.Enabled = true;
        }

        void checkForTime_Elapsed(object sender, ElapsedEventArgs e)
        {
            foreach (var subs in _clientPropertiesSubscriptions.Keys)
            {
                SendPingMessageToQueue(_clientPropertiesSubscriptions[subs], subs);
            }
        }
        

        public IConnection GetRabbitMqConnection()
        {
            ConnectionFactory connectionFactory = new ConnectionFactory();
            connectionFactory.HostName = _hostName;
            connectionFactory.UserName = _userName;
            connectionFactory.Password = _password;

            return connectionFactory.CreateConnection();
        }
        public void SetUpExchangeAndQueueForRpcDemo(IModel model)
        {
            model.ExchangeDeclare(_rpcExchangeConnectName, ExchangeType.Fanout, true);
            model.QueueDeclare(_rpcQueueConnectName, _durable, false, false, null);
            model.QueueBind(_rpcQueueConnectName, _rpcExchangeConnectName, "");

            model.ExchangeDeclare(_rpcExchangePingName, ExchangeType.Fanout, true);
            model.QueueDeclare(_rpcQueuePingName, _durable, false, false, null);
            model.QueueBind(_rpcQueuePingName, _rpcExchangePingName, "");
        }

        public string SendRpcConnectMessageToQueue(string message, IModel model, TimeSpan timeout)
        {
            if (string.IsNullOrEmpty(_responseQueue))
            {
                _responseQueue = model.QueueDeclare().QueueName;
            }

            if (_rpcConsumer == null)
            {
                _rpcConsumer = new QueueingBasicConsumer(model);
                model.BasicConsume(_responseQueue, true, _rpcConsumer);
            }

            string correlationId = Guid.NewGuid().ToString();

            IBasicProperties basicProperties = model.CreateBasicProperties();
            basicProperties.ReplyTo = _responseQueue;
            basicProperties.CorrelationId = correlationId;

            byte[] messageBytes = Encoding.UTF8.GetBytes(message);
            model.BasicPublish("", _rpcQueueConnectName, basicProperties, messageBytes);

            DateTime timeoutDate = DateTime.UtcNow + timeout;
            while (DateTime.UtcNow <= timeoutDate)
            {
                BasicDeliverEventArgs deliveryArguments = (BasicDeliverEventArgs)_rpcConsumer.Queue.Dequeue();
                if (deliveryArguments.BasicProperties != null && deliveryArguments.BasicProperties.CorrelationId == correlationId)
                {
                    string response = Encoding.UTF8.GetString(deliveryArguments.Body);
                    return response;
                }
            }
            throw new TimeoutException("No response before the timeout period.");
        }


        public void ReceiveRpcConnectMessage(IModel model)
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
                byte[] responseBytes = Encoding.UTF8.GetBytes("Ok");
                model.BasicPublish("", deliveryArguments.BasicProperties.ReplyTo, replyBasicProperties, responseBytes);
                model.BasicAck(deliveryArguments.DeliveryTag, false);

                createConsumer(model);
            }
        }

        //Adds a new queue and consumer for ping pong
        private void createConsumer(IModel model)
        {
            var tmpqueue = model.QueueDeclare().QueueName;
            var tmpConsumer = new QueueingBasicConsumer(model);

            model.BasicConsume(tmpqueue, true, tmpConsumer);
            
            IBasicProperties basicProperties = model.CreateBasicProperties();
            basicProperties.ReplyTo = _responseQueue;

            _clientPropertiesSubscriptions.Add(basicProperties,model);

        }

        private string SendPingMessageToQueue(IModel model, IBasicProperties basicProperties)
        {
            string correlationId = Guid.NewGuid().ToString();
            basicProperties.CorrelationId = correlationId;

            model.BasicPublish("", _rpcQueuePingName, basicProperties, Encoding.UTF8.GetBytes("Ping"));

            //addTimeout
            DateTime timeoutDate = DateTime.UtcNow.AddMinutes(2);
            while (DateTime.UtcNow <= timeoutDate)
            {
                BasicDeliverEventArgs deliveryArguments = (BasicDeliverEventArgs)_rpcConsumer.Queue.Dequeue();
                if (deliveryArguments.BasicProperties != null && deliveryArguments.BasicProperties.CorrelationId == correlationId)
                {
                    string response = Encoding.UTF8.GetString(deliveryArguments.Body);
                    return response;
                }
            }
            throw new TimeoutException("No response before the timeout period.");
        }

        public string SendRpcPingMessageToQueue(string message, IModel model, TimeSpan timeout)
        {

            if (string.IsNullOrEmpty(_responseQueue))
            {
                _responseQueue = model.QueueDeclare().QueueName;
            }

            if (_rpcConsumer == null)
            {
                _rpcConsumer = new QueueingBasicConsumer(model);
                model.BasicConsume(_responseQueue, true, _rpcConsumer);
            }

            string correlationId = Guid.NewGuid().ToString();

            IBasicProperties basicProperties = model.CreateBasicProperties();
            basicProperties.ReplyTo = _responseQueue;
            basicProperties.CorrelationId = correlationId;

            byte[] messageBytes = Encoding.UTF8.GetBytes(message);

            Console.WriteLine("Sending " + message);
            model.BasicPublish("", _rpcQueuePingName, basicProperties, messageBytes);

            DateTime timeoutDate = DateTime.UtcNow + timeout;
            while (DateTime.UtcNow <= timeoutDate)
            {
                BasicDeliverEventArgs deliveryArguments = (BasicDeliverEventArgs)_rpcConsumer.Queue.Dequeue();
                if (deliveryArguments.BasicProperties != null
                && deliveryArguments.BasicProperties.CorrelationId == correlationId)
                {
                    string response = Encoding.UTF8.GetString(deliveryArguments.Body);
                    return response;
                }
            }
            throw new TimeoutException("No response before the timeout period.");
        }

        public void ReceiveRpcPongMessage(IModel model)
        {
            model.BasicQos(0, 1, false);
            QueueingBasicConsumer consumer = new QueueingBasicConsumer(model);
            model.BasicConsume(_rpcQueuePingName, false, consumer);

            while (true)
            {
                BasicDeliverEventArgs deliveryArguments = consumer.Queue.Dequeue() as BasicDeliverEventArgs;
                Console.WriteLine("Received Ping");
                IBasicProperties replyBasicProperties = model.CreateBasicProperties();
                replyBasicProperties.CorrelationId = deliveryArguments.BasicProperties.CorrelationId;
                byte[] responseBytes = Encoding.UTF8.GetBytes("Pong");
                Console.WriteLine("Sending Pong");
                model.BasicPublish("", deliveryArguments.BasicProperties.ReplyTo, replyBasicProperties, responseBytes);
                model.BasicAck(deliveryArguments.DeliveryTag, false);
            }
        }
    }
}