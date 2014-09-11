using System.Collections.Generic;
using System.Threading.Tasks;

namespace Ncqrs.Messaging.Tests
{
    public class FakeSendingStrategy : ISendingStrategy
    {
        private readonly Queue<OutgoingMessage> _messages = new Queue<OutgoingMessage>();
        private IMessageService messageService;

        public FakeSendingStrategy(IMessageService messageService)
        {
            this.messageService = messageService;
        }

        public void Send(OutgoingMessage message)
        {
            var task = Task.Factory.StartNew(() => messageService.Process(message));
            task.Wait();
            //_messages.Enqueue(message);
        }

        public object DequeueMessage()
        {
            return _messages.Dequeue();
        }
    }
}