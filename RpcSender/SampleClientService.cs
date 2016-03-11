using System;
using System.Text;
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
        
        
        private string _rpcQueueConnectName = "RpcQueueConnect";
        private string _rpcExchangeConnectName = "RpcExchangeConnect";

        private QueueingBasicConsumer _rpcConsumer;
        private string _responseQueue;
       private string _rpcQueuePingName;
        
        private readonly IModel model;

        public SampleClientService()
        {
            ConnectionFactory connectionFactory = new ConnectionFactory
            {
                HostName = _hostName,
                UserName = _userName,
                Password = _password
            };

            model = connectionFactory.CreateConnection().CreateModel();
        }


        public string SendRpcConnectMessageToQueue(string message, TimeSpan timeout)
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
                    _rpcQueuePingName = response;
                    return response;
                }
            }
            throw new TimeoutException("No response before the timeout period.");
        }

        public void ReceiveRpcPongMessage()
        {
            model.BasicQos(0, 1, false);
            QueueingBasicConsumer consumer = new QueueingBasicConsumer(model);
            model.BasicConsume(_rpcQueuePingName, false, consumer);

            while (true)
            {
                BasicDeliverEventArgs deliveryArguments = consumer.Queue.Dequeue() as BasicDeliverEventArgs;
                IBasicProperties replyBasicProperties = model.CreateBasicProperties();
                replyBasicProperties.CorrelationId = deliveryArguments.BasicProperties.CorrelationId;
                string message = Encoding.UTF8.GetString(deliveryArguments.Body);
                Console.WriteLine("Received message: "+ message +"  with Correlation Id: " + replyBasicProperties.CorrelationId + "for queue " + deliveryArguments.BasicProperties.ReplyTo );
                byte[] responseBytes = Encoding.UTF8.GetBytes("Pong");
                Console.WriteLine("Sending Pong");
                model.BasicPublish("", deliveryArguments.BasicProperties.ReplyTo, replyBasicProperties, responseBytes);
                model.BasicAck(deliveryArguments.DeliveryTag, false);
            }
        }

        public void SetUpExchangeAndQueueForRpcDemo()
        {
            model.ExchangeDeclare(_rpcExchangeConnectName, ExchangeType.Fanout, true);
            model.QueueDeclare(_rpcQueueConnectName, true, false, false, null);
            model.QueueBind(_rpcQueueConnectName, _rpcExchangeConnectName, "");
        }
    }
}
