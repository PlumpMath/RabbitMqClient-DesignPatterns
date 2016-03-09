using System;
using System.Text;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using RabbitMQ.Client.MessagePatterns;

namespace RabbitMqService
{
    public class AmqpMessagingService
    {
        private string _hostName = "localhost";
        private string _userName = "guest";
        private string _password = "guest";
        private string _exchangeName = "";
        private string _oneWayMessageQueueName = "OneWayMessageQueue";
        private bool _durable = true;
        private string _workerQueueDemoQueueName = "WorkerQueueDemoQueue";
        private string _publishSubscribeExchangeName = "PublishSubscribeExchange";
        private string _publishSubscribeQueueOne = "PublishSubscribeQueueOne";
        private string _publishSubscribeQueueTwo = "PublishSubscribeQueueTwo";
        private string _rpcQueueName = "RpcQueue";
        private string _rpcExchangeName = "RpcExchange";
        private QueueingBasicConsumer _rpcConsumer;
        private string _responseQueue;

        public IConnection GetRabbitMqConnection()
        {
            ConnectionFactory connectionFactory = new ConnectionFactory();
            connectionFactory.HostName = _hostName;
            connectionFactory.UserName = _userName;
            connectionFactory.Password = _password;

            return connectionFactory.CreateConnection();
        }

        public void SetUpQueueForOneWayMessageDemo(IModel model)
        {
            model.QueueDeclare(_oneWayMessageQueueName, _durable, false, false, null);
        }

        public void SendOneWayMessage(string message, IModel model)
        {
            IBasicProperties basicProperties = model.CreateBasicProperties();
            basicProperties.SetPersistent(_durable);
            byte[] messageBytes = Encoding.UTF8.GetBytes(message);
            model.BasicPublish(_exchangeName, _oneWayMessageQueueName, basicProperties, messageBytes);
        }

        public void ReceiveOneWayMessages(IModel model)
        {
            model.BasicQos(0, 1, false); //basic quality of service
            QueueingBasicConsumer consumer = new QueueingBasicConsumer(model);
            model.BasicConsume(_oneWayMessageQueueName, false, consumer);
            while (true)
            {
                BasicDeliverEventArgs deliveryArguments = consumer.Queue.Dequeue() as BasicDeliverEventArgs;
                String message = Encoding.UTF8.GetString(deliveryArguments.Body);
                Console.WriteLine("Message received: {0}", message);
                model.BasicAck(deliveryArguments.DeliveryTag, false);
            }
        }

        public void ReceiveWorkerQueueMessages(IModel model)
        {
            model.BasicQos(0, 1, false); //basic quality of service
            QueueingBasicConsumer consumer = new QueueingBasicConsumer(model);
            model.BasicConsume(_workerQueueDemoQueueName, false, consumer);
            while (true)
            {
                BasicDeliverEventArgs deliveryArguments = consumer.Queue.Dequeue() as BasicDeliverEventArgs;
                String message = Encoding.UTF8.GetString(deliveryArguments.Body);
                Console.WriteLine("Message received: {0}", message);
                model.BasicAck(deliveryArguments.DeliveryTag, false);
            }
        }

        public void SetUpQueueForWorkerQueueDemo(IModel model)
        {
            model.QueueDeclare(_workerQueueDemoQueueName, _durable, false, false, null);
        }

        public void SendMessageToWorkerQueue(string message, IModel model)
        {
            IBasicProperties basicProperties = model.CreateBasicProperties();
            basicProperties.SetPersistent(_durable);
            byte[] messageBytes = Encoding.UTF8.GetBytes(message);
            model.BasicPublish(_exchangeName, _workerQueueDemoQueueName, basicProperties, messageBytes);
        }

        public void SetUpExchangeAndQueuesForDemo(IModel model)
        {
            model.ExchangeDeclare(_publishSubscribeExchangeName, ExchangeType.Fanout, true);
            model.QueueDeclare(_publishSubscribeQueueOne, true, false, false, null);
            model.QueueDeclare(_publishSubscribeQueueTwo, true, false, false, null);
            model.QueueBind(_publishSubscribeQueueOne, _publishSubscribeExchangeName, "");
            model.QueueBind(_publishSubscribeQueueTwo, _publishSubscribeExchangeName, "");
        }

        public void SendMessageToPublishSubscribeQueue(string message, IModel model)
        {
            IBasicProperties basicProperties = model.CreateBasicProperties();
            basicProperties.SetPersistent(_durable);
            byte[] messageBytes = Encoding.UTF8.GetBytes(message);
            model.BasicPublish(_publishSubscribeExchangeName, "", basicProperties, messageBytes);
        }

        public void ReceivePublishSubscribeMessageReceiverOne(IModel model)
        {
            model.BasicQos(0, 1, false);
            Subscription subscription = new Subscription(model, _publishSubscribeQueueOne, false);
            while (true)
            {
                BasicDeliverEventArgs deliveryArguments = subscription.Next();
                String message = Encoding.UTF8.GetString(deliveryArguments.Body);
                Console.WriteLine("Message from queue: {0}", message);
                subscription.Ack(deliveryArguments);
            }
        }

        public void ReceivePublishSubscribeMessageReceiverTwo(IModel model)
        {
            model.BasicQos(0, 1, false);
            Subscription subscription = new Subscription(model, _publishSubscribeQueueTwo, false);
            while (true)
            {
                BasicDeliverEventArgs deliveryArguments = subscription.Next();
                String message = Encoding.UTF8.GetString(deliveryArguments.Body);
                Console.WriteLine("Message from queue: {0}", message);
                subscription.Ack(deliveryArguments);
            }
        }

        public void SetUpExchangeAndQueueForRpcDemo(IModel model)
        {
            model.ExchangeDeclare(_rpcExchangeName, ExchangeType.Fanout, true);
            model.QueueDeclare(_rpcQueueName, _durable, false, false, null);
            model.QueueBind(_rpcQueueName, _rpcExchangeName, "");
        }

        public string SendRpcMessageToQueue(string message, IModel model, TimeSpan timeout)
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
            model.BasicPublish("", _rpcQueueName, basicProperties, messageBytes);

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

        public void ReceiveRpcMessage(IModel model)
        {
            model.BasicQos(0, 1, false);
            QueueingBasicConsumer consumer = new QueueingBasicConsumer(model);
            model.BasicConsume(_rpcQueueName, false, consumer);

            while (true)
            {
                BasicDeliverEventArgs deliveryArguments = consumer.Queue.Dequeue() as BasicDeliverEventArgs;
                string message = Encoding.UTF8.GetString(deliveryArguments.Body);
                Console.WriteLine("Message: {0} ; {1}", message, " Enter your response: ");
                string response = Console.ReadLine();
                IBasicProperties replyBasicProperties = model.CreateBasicProperties();
                replyBasicProperties.CorrelationId = deliveryArguments.BasicProperties.CorrelationId;
                byte[] responseBytes = Encoding.UTF8.GetBytes(response);
                model.BasicPublish("", deliveryArguments.BasicProperties.ReplyTo, replyBasicProperties, responseBytes);
                model.BasicAck(deliveryArguments.DeliveryTag, false);
            }
        }

    }
}