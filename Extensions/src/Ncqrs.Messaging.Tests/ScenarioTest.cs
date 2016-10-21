using System;
using Ncqrs.Domain;
using Ncqrs.Eventing.ServiceModel.Bus;
using Xunit;
using Ncqrs.Eventing.Sourcing;
using System.Threading;
using Ncqrs.Commanding.ServiceModel;
using Ncqrs.Commanding;
using Ncqrs.Commanding.CommandExecution.Mapping.Attributes;

namespace Ncqrs.Messaging.Tests
{
    public class ScenarioTest
    {
        public ScenarioTest()
        {
            NcqrsEnvironment.Deconfigure();
        }

        [Fact]
        public void New_cargo_handling_event_is_registrered()
        {
            var cargoId = Guid.NewGuid();
            var firstEventId = Guid.NewGuid();
            var messageService = new MessageService();
            messageService.UseReceivingStrategy(
                new ConditionalReceivingStrategy(
                    x => x.GetType() == typeof (BookCargoMessage),
                    new MappingReceivingStrategy<BookCargoMessage>(
                        x => new IncomingMessage()
                                 {
                                     MessageId = x.MessageId,
                                     Payload = x,
                                     ProcessingRequirements = MessageProcessingRequirements.RequiresNew,
                                     ReceiverId = x.CargoId,
                                     ReceiverType = typeof (Cargo),
                                     SenderId = "Client"
                                 })));

            //messageService
            //    .ForIncomingMessage<RegisterHandlingEventTransportMesasge>()
            //    .AsPayloadUse(x => x)
            //    .AsMessageIdUse(x => x.MessageId)
            //    .AsReceiverIdUse(x => x.EventId)
            //    .AsSenderUse("Client");            

            //messageService
            //    .Map<RegisterHandlingEventTransportMesasge>().To<RegisterHandlingEventMessage>();                            

            messageService.UseReceivingStrategy(
                new ConditionalReceivingStrategy(
                    x => x.GetType() == typeof (RegisterHandlingEventMesasge),
                    new MappingReceivingStrategy<RegisterHandlingEventMesasge>(
                        x => new IncomingMessage()
                                 {
                                     MessageId = x.MessageId,
                                     Payload = x,
                                     ProcessingRequirements = MessageProcessingRequirements.RequiresExisting,
                                     ReceiverId = x.EventId,
                                     ReceiverType = typeof (HandlingEvent),
                                     SenderId = "Client"
                                 })));
            messageService.UseReceivingStrategy(new ConditionalReceivingStrategy(x => true, new LocalReceivingStrategy()));

            var messageSendingEventHandler = new MessageSendingEventHandler();
            var sendingStrategy = new FakeSendingStrategy(messageService);
            messageSendingEventHandler.UseStrategy(new ConditionalSendingStrategy(x => true, sendingStrategy));

            var bus = ((InProcessEventBus)NcqrsEnvironment.Get<IEventBus>());
            bus.RegisterHandler(messageSendingEventHandler);

            CommandService service = new CommandService();
            service.RegisterExecutorsInAssembly(this.GetType().Assembly);

            //Book new cargo
            messageService.Process(new BookCargoMessage
                                      {
                                          CargoId = cargoId,
                                          MessageId = Guid.NewGuid(),                                          
                                      });

            service.Execute(new BeginHandlingCommand() { Id = Guid.NewGuid(), cargoId = cargoId });

            //Register new handling event
            //messageService.Process(new RegisterHandlingEventMesasge
            //                          {
            //                              EventId = firstEventId,
            //                              MessageId = Guid.NewGuid(),
            //                              CargoId = cargoId
            //                          });

            //Process message from event to cargo
            //object message = sendingStrategy.DequeueMessage();
            //messageService.Process(message);

            //Thread.Sleep(2000); // The FakeSendingStrategy switches threads to process the message, so we need to wait for it to complete.

            using (var uow = NcqrsEnvironment.Get<IUnitOfWorkFactory>().CreateUnitOfWork(Guid.NewGuid()))
            {
                var cargo = (Cargo)uow.GetById(typeof(Cargo),cargoId, null);
                Assert.Equal(1, cargo.HandlingEventCount);
            }
        }

        [Serializable]
        public class RegisterHandlingEventMesasge
        {
            public Guid MessageId { get; set; }
            public Guid EventId { get; set; }
            public Guid CargoId { get; set; }
        }

        [MapsToAggregateRootConstructor(typeof(HandlingEvent))]
        public class BeginHandlingCommand : CommandBase
        {
            public Guid Id { get; set; }
            public Guid cargoId { get; set; }
        }

        [Serializable]
        public class BookCargoMessage
        {
            public Guid MessageId { get; set; }
            public Guid CargoId { get; set; }
        }

        [MapsToAggregateRootConstructor(typeof(Cargo))]
        public class BookCargoCommand : CommandBase
        {
            public Guid Id { get; set; }
        }

        [Serializable]
        public class CargoWasHandledMessage
        {
        }

        public class HandlingBeganEvent { }

        public class HandlingEvent : MessagingAggregateRoot
        {
            private Guid _cargoId;

            public HandlingEvent()
            {                
            }

            public HandlingEvent(Guid id, Guid cargoId) : base(id)
            {
                ApplyEvent(new HandlingEventRegistered
                {
                    CargoId = cargoId
                });

                To().Aggregate<Cargo>(_cargoId)
                    .Ensuring(MessageProcessingRequirements.RequiresExisting)
                    .Send(new CargoWasHandledMessage());
            }

            private void OnHandlingEventRegistered(HandlingEventRegistered @event)
            {
                _cargoId = @event.CargoId;
            }
        }

        public class Cargo : MessagingAggregateRoot,
           IMessageHandler<BookCargoMessage>,
           IMessageHandler<CargoWasHandledMessage>
        {
            private int _handlingEventCount;

            public int HandlingEventCount
            {
                get { return _handlingEventCount; }
            }

            public Cargo(Guid id) : base(id)
            {                
            }

            public Cargo() : base()
            {                
            }

            public void Handle(BookCargoMessage message)
            {
                ApplyEvent(new CargoBooked());
            }

            private void OnCargoBooked(CargoBooked @event)
            {                
            }

            public void Handle(CargoWasHandledMessage message)
            {
                ApplyEvent(new CargoHandled());
            }

            private void OnCargoHandled(CargoHandled @event)
            {
                _handlingEventCount++;
            }
        }

        public class HandlingEventRegistered
        {
            public Guid CargoId { get; set; }
        }

        public class CargoBooked
        {            
        }

        public class CargoHandled
        {
        }
    }
}