using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.ServiceModel;
using System.Text;
using System.Threading;

using Ncqrs;
using Ncqrs.CommandService;
using Ncqrs.EventBus;
using Ncqrs.Eventing.ServiceModel.Bus;
using Ncqrs.Eventing.Sourcing;
using Ncqrs.Eventing.Storage;
using Ncqrs.Eventing.Storage.SQL;

namespace MyNotes.ApplicationService
{
    static class Program
    {
        public static Func<IBrowsableElementStore> GetBrowsableEventStore = GetBuiltInBrowsableElementStore;

        static void Main(string[] args)
        {
            Console.WindowHeight = 30;
            Console.WindowWidth = 150;

            // Setup the bus that will actually run the EventHandlers.
            var bus = new InProcessEventBus(true);
            bus.RegisterAllHandlersInAssembly(typeof(MyNotes.Denormalizers.NoteDenormalizer).Assembly);

            // This is what will store the events to the DB as poor man's message queue
            var eventStore = GetBrowsableEventStore();
            var buffer = new InMemoryBufferedBrowsableElementStore(eventStore, 20 /*magic number found in ThresholdFetchPolicy*/);
            BootStrapper.BootUp(buffer);

            // Pipelines are what will process the persisted events onto the final bus, which
            // will result in the EventHandlers running.
            var pipeline = Pipeline.Create("Default", new EventBusProcessor(bus), buffer);
            pipeline.Start();

            // The command Service Host ends up using the pipelined eventStore
            // which is the default instance for the bus in the environment (set in the BoostStrapper)
            var commandServiceHost = new ServiceHost(typeof(CommandWebService));
            commandServiceHost.Open();

            Console.ReadLine();

            commandServiceHost.Close();
            pipeline.Stop();
        }

        private static IBrowsableElementStore GetBuiltInBrowsableElementStore()
        {
            var connectionString = ConfigurationManager.ConnectionStrings["MyNotes Event Store"].ConnectionString;
            var browsableEventStore = new MsSqlServerEventStoreElementStore(connectionString);

            return browsableEventStore;
        }
    }
}
