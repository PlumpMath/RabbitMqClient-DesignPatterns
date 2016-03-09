using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using RabbitMqService;
using RabbitMQ.Client;

namespace PublishSubscribeReceiver
{
    class Program
    {
        static void Main(string[] args)
        {
            AmqpMessagingService messagingService = new AmqpMessagingService();
            IConnection connection = messagingService.GetRabbitMqConnection();
            IModel model = connection.CreateModel();
            messagingService.ReceivePublishSubscribeMessageReceiverTwo(model);
        }
    }
}
