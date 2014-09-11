using Ncqrs.Commanding.ServiceModel;
using Ncqrs.Config;
using Ncqrs.Eventing.ServiceModel.Bus;
using Ncqrs.Commanding.CommandExecution.Mapping.Attributes;
using Ncqrs.Spec;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using Ncqrs.Eventing.Storage;
using Ncqrs.Eventing.Storage.SQLite;

namespace Ncqrs.Messaging.Tests
{
    public class CommandExecutionTests : BigBangTestFixture<Ncqrs.Messaging.Tests.ScenarioTest.BeginHandlingCommand>
    {
        private readonly Guid cargoId = Guid.NewGuid();

        public CommandExecutionTests()
        {
        }

        protected override void SetupDependencies()
        {
            MessagingEnvironmentConfiguration.Configure();
            base.SetupDependencies();
            var messagingService = NcqrsEnvironment.Get<IMessageService>();
            if (messagingService != null)
            {
                var messageSendingEventHandler = new MessageSendingEventHandler();
                var sendingStrategy = new FakeSendingStrategy(messagingService);
                messageSendingEventHandler.UseStrategy(new ConditionalSendingStrategy(x => true, sendingStrategy));

                IEventBus eventBus = NcqrsEnvironment.Get<IEventBus>();
                ((InProcessEventBus)eventBus).RegisterHandler(messageSendingEventHandler);
            }

            messagingService.Process(new Ncqrs.Messaging.Tests.ScenarioTest.BookCargoMessage
            {
                CargoId = cargoId,
                MessageId = Guid.NewGuid(),
            });
        }

        protected override void RegisterFakesInConfiguration(Spec.Fakes.EnvironmentConfigurationWrapper configuration)
        {
            base.RegisterFakesInConfiguration(configuration);
        }

        protected override Ncqrs.Messaging.Tests.ScenarioTest.BeginHandlingCommand WhenExecuting()
        {
            return new Ncqrs.Messaging.Tests.ScenarioTest.BeginHandlingCommand
            {
                Id = Guid.NewGuid(),
                cargoId = cargoId
            };
        }

        [Then]
        public void it_should_not_throw()
        {
            var exception = CaughtException;
            Assert.IsNull(exception);
        }
    }

    public class MessagingEnvironmentConfiguration : IEnvironmentConfiguration
    {
        public static void Configure()
        {
            if (NcqrsEnvironment.IsConfigured) return;
            var cfg = new MessagingEnvironmentConfiguration();
            NcqrsEnvironment.Configure(cfg);
        }

        private static ICommandService InitializeCommandService()
        {
            var service = new CommandService();

            var testAssembly = typeof(ScenarioTest).Assembly;
            service.RegisterExecutorsInAssembly(testAssembly);

            return service;
        }

        private static IMessageService InitializeMessageService()
        {
            var messageService = new MessageService();

            messageService.UseReceivingStrategy(
                new ConditionalReceivingStrategy(
                    x => x.GetType() == typeof(Ncqrs.Messaging.Tests.ScenarioTest.BookCargoMessage),
                    new MappingReceivingStrategy<Ncqrs.Messaging.Tests.ScenarioTest.BookCargoMessage>(
                        x => new IncomingMessage()
                        {
                            MessageId = x.MessageId,
                            Payload = x,
                            ProcessingRequirements = MessageProcessingRequirements.RequiresNew,
                            ReceiverId = x.CargoId,
                            ReceiverType = typeof(Ncqrs.Messaging.Tests.ScenarioTest.Cargo),
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
                    x => x.GetType() == typeof(Ncqrs.Messaging.Tests.ScenarioTest.RegisterHandlingEventMesasge),
                    new MappingReceivingStrategy<Ncqrs.Messaging.Tests.ScenarioTest.RegisterHandlingEventMesasge>(
                        x => new IncomingMessage()
                        {
                            MessageId = x.MessageId,
                            Payload = x,
                            ProcessingRequirements = MessageProcessingRequirements.RequiresExisting,
                            ReceiverId = x.EventId,
                            ReceiverType = typeof(Ncqrs.Messaging.Tests.ScenarioTest.HandlingEvent),
                            SenderId = "Client"
                        })));

            //messagingService.UseReceivingStrategy(IssueReferralMessageStrategy.ReceivingStrategy());
            messageService.UseReceivingStrategy(new ConditionalReceivingStrategy(x => true, new LocalReceivingStrategy()));

            return messageService;
        }

        private readonly ICommandService commandService;
        private readonly IMessageService messageService;
        private readonly IEventStore eventStore;

        public MessagingEnvironmentConfiguration()
        {
            messageService = InitializeMessageService();
            commandService = InitializeCommandService();
            var conn = @"Data Source=d:\dev\mydb.db;";
            SQLiteEventStore.EnsureDatabaseExists(conn);
            eventStore = new Ncqrs.Eventing.Storage.SQLite.SQLiteEventStore(new Ncqrs.Eventing.Storage.SQLite.DefaultSQLiteContext(conn + ";Version=3;"));
        }

        public bool TryGet<T>(out T result) where T : class
        {
            result = null;
            if (typeof(T) == typeof(ICommandService))
                result = (T)commandService;
            if (typeof(T) == typeof(IMessageService))
                result = (T)messageService;
            if (typeof(T) == typeof(IEventStore))
                result = (T)eventStore;
            return result != null;
        }
    }
}
