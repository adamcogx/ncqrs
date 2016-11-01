using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using MongoDB.Bson;
using MongoDB.Driver;
using Ncqrs.EventBus;
using System.Reflection;
using Ncqrs.Eventing.Storage.MongoDB;
	
namespace Ncqrs.EventBus
{
	public class MongoDBEventStoreElementStore : IBrowsableElementStore
	{
		private readonly MongoDBEventStore wrappedStore;
		private Guid? lastEventId;
		private readonly IMongoDatabase database;
		public const string PIPELINETABLE = "PipelineState";
		private const string PIPELINESEQUENCE = "Pipeline";
		protected static readonly ILog Log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

		public MongoDBEventStoreElementStore(string databaseUrl, string databaseName, IClassMapBuilder builder = null)
		{
			wrappedStore = new MongoDBEventStore(databaseUrl, databaseName, builder);

			var client = new MongoClient(databaseUrl);
			database = client.GetDatabase(databaseName);

			EnsureCollections(database);
		}

		public MongoDBEventStoreElementStore(string databaseUrl, string databaseName, MongoDBEventStore wrappedStore)
		{
			this.wrappedStore = wrappedStore;

			var client = new MongoClient(databaseUrl);
			database = client.GetDatabase(databaseName);

			EnsureCollections(database);
		}

		private void EnsureCollections(IMongoDatabase database)
		{
			var collections = database.ListCollections().ToList().Select(x => x.GetValue("name").AsString).ToList();

			if (!collections.Contains(PIPELINETABLE)) {
				Log.InfoFormat("Creating {0} Collection", PIPELINETABLE);
				database.CreateCollection(PIPELINETABLE);
			}

			var coll = database.GetCollection<EventSequence>(MongoDBEventStore.SEQUENCETABLE);
			if (!coll.Find(x => x.Name == PIPELINESEQUENCE).Any()) {
				Log.InfoFormat("Adding {0} sequence", PIPELINESEQUENCE);
				EventSequence seq = new EventSequence {
					Name = PIPELINESEQUENCE,
					Sequence = 0
				};

				coll.InsertOne(seq);
			}
		}

		public IEnumerable<IProcessingElement> Fetch(string pipelineName, int maxCount)
		{
			if (!lastEventId.HasValue) {
				lastEventId = GetLastProcessedEvent(pipelineName);
			}

			var result = wrappedStore.GetEventsAfter(lastEventId.HasValue ? lastEventId.Value : (Guid?)null, maxCount);
			foreach (var evnt in result) {
				lastEventId = evnt.EventIdentifier;
				yield return new SourcedEventProcessingElement(evnt);
			}
		}

		// SELECT TOP 1 [LastProcessedEventId] FROM [PipelineState] WHERE [PipelineName] = @PipelineName ORDER BY [BatchId] DESC
		private Guid? GetLastProcessedEvent(string pipelineName)
		{
			var coll = database.GetCollection<PipelineStatus>(PIPELINETABLE);
			var sort = Builders<PipelineStatus>.Sort.Descending(x => x.Id);
			var result = coll.Find(x => x.PipelineName == pipelineName).Sort(sort).Limit(1).FirstOrDefault();

			return result != null ? result.LastProcessedEventId : (Guid?)null;
		}

		public void MarkLastProcessedElement(string pipelineName, IProcessingElement processingElement)
		{
			var typedElement = (SourcedEventProcessingElement)processingElement;
			var pipeline = new PipelineStatus() { Id = wrappedStore.GetNextSequence(PIPELINESEQUENCE), PipelineName = pipelineName, LastProcessedEventId = typedElement.Event.EventIdentifier };

			var coll = database.GetCollection<PipelineStatus>(PIPELINETABLE);
			coll.InsertOne(pipeline);
		}
	}
}
