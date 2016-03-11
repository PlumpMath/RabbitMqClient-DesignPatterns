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



            var publisherSettings = new PublisherSettings(ConnectSettings,Reconnect, exchange, false);
            var publisher = new Publisher(publisherSettings);
            publisher.Start();

            publisher.PublishNew(message, _rpcQueueConnectName, correlationId, replyToQueueName);

            

            var connectConsumerSettings = new ConsumerSettings(ConnectSettings,
                Reconnect, new Queue(replyToQueueName), null, 0, 1, 1);
            var connectConsumer = new Consumer(connectConsumerSettings);
            connectConsumer.Start(HandleReceivedConnectMessage);

            


            //            if (string.IsNullOrEmpty(_responseQueue))
            //            {
            //                _responseQueue = model.QueueDeclare().QueueName;
            //            }
            //
            //            if (_rpcConsumer == null)
            //            {
            //                _rpcConsumer = new QueueingBasicConsumer(model);
            //                model.BasicConsume(_responseQueue, true, _rpcConsumer);
            //            }
            //
            //            string correlationId = Guid.NewGuid().ToString();
            //
            //            IBasicProperties basicProperties = model.CreateBasicProperties();
            //            basicProperties.ReplyTo = _responseQueue;
            //            basicProperties.CorrelationId = correlationId;
            //
            //            byte[] messageBytes = Encoding.UTF8.GetBytes(message);
            //            model.BasicPublish("", _rpcQueueConnectName, basicProperties, messageBytes);
            //
            //            DateTime timeoutDate = DateTime.UtcNow + timeout;
            //            while (DateTime.UtcNow <= timeoutDate)
            //            {
            //                BasicDeliverEventArgs deliveryArguments = (BasicDeliverEventArgs)_rpcConsumer.Queue.Dequeue();
            //                if (deliveryArguments.BasicProperties != null && deliveryArguments.BasicProperties.CorrelationId == correlationId)
            //                {
            //                    string response = Encoding.UTF8.GetString(deliveryArguments.Body);
            //                    _rpcQueuePingName = response;
            //                    var dirtyFix = _rpcQueuePingName.Substring(1, _rpcQueuePingName.Length - 2);
            //                    model.QueueDeclare(dirtyFix, false, false, true, null);
            //                    return response;
            //                }
            //            }
            //            throw new TimeoutException("No response before the timeout period.");
        }

        private Task<IMessage> HandleReceivedPingMessage(IMessage data)
        {

            var connectQueue = new Queue(data.ReplyTo);

            var exchange = new Exchange("", Igc.RabbitMq.ExchangeType.Direct);
            exchange.Queues = new List<Queue> { connectQueue };

            var publisherSettings = new PublisherSettings(ConnectSettings, Reconnect, exchange, false);
            var publisher = new Publisher(publisherSettings);
            publisher.Start();

            publisher.PublishNew("Ping", data.ReplyTo, data.CorrelationId, data.ReplyTo);

            Console.WriteLine("Sending pong for: {0} with correlation ID: {1}", data.ReplyTo,data.CorrelationId );

            return Task.FromResult(data);
        }

        private Task<IMessage> HandleReceivedConnectMessage(IMessage data)
        {
            Console.WriteLine("Revived connect {0}", data.Payload);

            var dirtyFix = data.Payload.Substring(1, data.Payload.Length - 2);

            var tempQueue = new Queue(dirtyFix);

            var pingConsumerSettings = new ConsumerSettings(ConnectSettings,
                Reconnect, tempQueue, null, 0, 1, 1);
            var pingConsumer = new Consumer(pingConsumerSettings);
            pingConsumer.Start(HandleReceivedPingMessage);


            return Task.FromResult(data);
        }

//        public void ReceiveRpcPongMessage()
//        {
//            model.BasicQos(0, 1, false);
//            QueueingBasicConsumer consumer = new QueueingBasicConsumer(model);
//            
//            var dirtyFix = _rpcQueuePingName.Substring(1, _rpcQueuePingName.Length - 2);
//            model.BasicConsume(dirtyFix, false, consumer);
//
//            while (true)
//            {
//                BasicDeliverEventArgs deliveryArguments = consumer.Queue.Dequeue() as BasicDeliverEventArgs;
//                IBasicProperties replyBasicProperties = model.CreateBasicProperties();
//                replyBasicProperties.CorrelationId = deliveryArguments.BasicProperties.CorrelationId;
//                string message = Encoding.UTF8.GetString(deliveryArguments.Body);
//                Console.WriteLine("Received message: "+ message +"  with Correlation Id: " + replyBasicProperties.CorrelationId + "for queue " + deliveryArguments.BasicProperties.ReplyTo );
//                byte[] responseBytes = Encoding.UTF8.GetBytes("Pong");
//                Console.WriteLine("Sending Pong");
//                model.BasicPublish("", deliveryArguments.BasicProperties.ReplyTo, replyBasicProperties, responseBytes);
//                model.BasicAck(deliveryArguments.DeliveryTag, false);
//            }
//        }
    }
}
