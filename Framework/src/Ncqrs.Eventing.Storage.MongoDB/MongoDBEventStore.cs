using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization.Formatters.Binary;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Driver;
using Ncqrs.Eventing.Sourcing;

namespace Ncqrs.Eventing.Storage.MongoDB
{
	public class MongoDBEventStore : IEventStore, ISnapshotStore
	{
		public const string DEFAULT_DATABASE = "EventStore";
		public const string DEFAULT_SERVER_URI = "mongodb://127.0.0.1:27017";
		public const string EVENTSOURCETABLE = "EventSources";
		public const string EVENTTABLE = "DomainEvents";
		public const string SNAPSHOTTABLE = "Snapshots";
		public const string SEQUENCETABLE = "EventSequences";
		private const string DEFAULTSEQUENCE = "Default";

		protected static readonly ILog Log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);
		protected readonly IMongoDatabase database;
		private static MethodInfo maker = typeof(MongoDBEventStore).GetMethod("MakeStoredEvent", BindingFlags.NonPublic | BindingFlags.Static);
		private static object registerLock = new object();

		private IClassMapBuilder classMapBuilder = null;

		private bool initialized;

		public MongoDBEventStore(IClassMapBuilder builder = null)
			: this(DEFAULT_SERVER_URI, DEFAULT_DATABASE, builder)
		{
			AppDomain.CurrentDomain.AssemblyResolve += (sender, eventArgs) => {
				if (eventArgs.Name.Contains("DynamicSnapshot")) {
					var ass = Assembly.LoadFrom("DynamicSnapshot.dll");
					//foreach (var typeInfo in ass.DefinedTypes) {
					//	var type = typeInfo.AsType();
					//	var classMap = new BsonClassMap(type);
					//	classMap.AutoMap();

					//	BsonClassMap.RegisterClassMap(classMap);
					//}
					return ass;
				}

				return null;
			};
		}

		public MongoDBEventStore(string databaseUri, string databaseName, IClassMapBuilder builder = null)
		{
			MongoClient client = new MongoClient(new MongoClientSettings() { WriteConcern = WriteConcern.Acknowledged });
			this.database = client.GetDatabase(databaseName);
			this.classMapBuilder = builder;

			SetupCollections();
		}

		public Ncqrs.Eventing.Sourcing.Snapshotting.Snapshot GetSnapshot(Guid eventSourceId, long maxVersion)
		{
			var filterSrc = Builders<Snapshot>.Filter;
			var filter = filterSrc.Eq(x => x.EventSourceId, eventSourceId);
			var sort = Builders<Snapshot>.Sort.Descending(x => x.Version);

			var coll = database.GetCollection<Snapshot>(SNAPSHOTTABLE);
			var result = coll.Find(filter).Sort(sort).FirstOrDefault();

			if (result != null) {
				using (var buffer = new MemoryStream(result.Payload)) {
					var formatter = new BinaryFormatter();
					var payload = formatter.Deserialize(buffer);
					var theSnapshot = new Ncqrs.Eventing.Sourcing.Snapshotting.Snapshot(eventSourceId, result.Version, payload);

					// QUESTION: Does it make sense to have this check performed in the SQL Query that way
					// an older snapshot could be returned if it does exist?
					return theSnapshot.Version > maxVersion ? null : theSnapshot;
				}
			}

			return null;
		}

		public CommittedEventStream ReadFrom(Guid id, long minVersion, long maxVersion)
		{
			EnsureEventTypes();
			var events = new List<CommittedEvent>();

			//var filter = Builders<DomainEvent>.Filter.Eq(x => x.AggregateId, id);
			//var project = Builders<DomainEvent>.Projection.Expression(x => new {
			//	Events = x.Events.Where(e => minVersion <= e.Sequence && e.Sequence <= maxVersion)
			//});
			var coll = database.GetCollection<DomainEvent>(EVENTTABLE);
			var sort = Builders<DomainEvent>.Sort.Ascending(x => x.EventSourceId).Ascending(x => x.Version);

			var results = coll.Find<DomainEvent>(x => minVersion <= x.Sequence && x.Sequence <= maxVersion).Sort(sort);

			foreach (var result in results.ToEnumerable()) {
					events.Add(ReadEvent(result));
			}

			return new CommittedEventStream(id, events.OrderBy(x => x.EventSequence));
		}

		public void SaveSnapshot(Ncqrs.Eventing.Sourcing.Snapshotting.Snapshot snapshot)
		{
			using (var dataStream = new MemoryStream()) {
				var formatter = new BinaryFormatter();
				formatter.Serialize(dataStream, snapshot.Payload);
				byte[] data = dataStream.ToArray();

				Snapshot dbSnapshot = new Snapshot {
					EventSourceId = snapshot.EventSourceId,
					Version = snapshot.Version,
					Payload = data
				};

				var coll = database.GetCollection<Snapshot>(SNAPSHOTTABLE);
				coll.InsertOne(dbSnapshot);
			}
		}

		public void Store(UncommittedEventStream eventStream)
		{
			EnsureEventTypes();

			if (!eventStream.Any())
				return;

			if (eventStream.HasSingleSource) {
				var firstEvent = eventStream.First();
				var newVersion = firstEvent.InitialVersionOfEventSource + eventStream.Count();

				StoreEventsFromSource(eventStream.SourceId, newVersion, eventStream);
			} else {
				StoreMultipleSources(eventStream);
			}
		}

		public IEnumerable<CommittedEvent> GetEventsAfter(Guid? eventId, int maxCount)
		{
			EnsureEventTypes();

			var coll = database.GetCollection<DomainEvent>(EVENTTABLE);

			long? sequenceId = null;

			if (eventId.HasValue) {
				var filter = Builders<DomainEvent>.Filter.Eq(x => x.EventId, eventId.Value);
				sequenceId = coll.Find(filter).Project(x => (long?)x.SequentialId).FirstOrDefault();
			}

			FilterDefinition<DomainEvent> resultFilter = null;

			if (sequenceId.HasValue) {
				resultFilter = Builders<DomainEvent>.Filter.Gt(x => x.SequentialId, sequenceId.Value);
			} else {
				resultFilter = Builders<DomainEvent>.Filter.Empty;
			}

			var resultSort = Builders<DomainEvent>.Sort.Ascending(x => x.SequentialId);
			var results = coll.Find(resultFilter).Sort(resultSort).Limit(maxCount).ToList().Select(x => ReadEvent(x));

			return results;
		}

		private CommittedEvent ReadEvent(DomainEvent x)
		{
			StoredEvent evt = maker.MakeGenericMethod(x.Event.GetType()).Invoke(null, new object[] { x }) as StoredEvent;
			var committedEvt = new CommittedEvent(Guid.Empty, evt.EventIdentifier, evt.EventSourceId, evt.EventSequence, evt.EventTimeStamp, x.Event, evt.EventVersion);

			if (committedEvt is ISourcedEvent) {
				((ISourcedEvent)committedEvt).InitializeFrom(evt);
			}

			return committedEvt;
		}

		private static StoredEvent<T> MakeStoredEvent<T>(DomainEvent evt)
		{
			return new StoredEvent<T>(
				evt.EventId,
				evt.Timestamp,
				evt.Event.GetType().Name,
				Version.Parse(evt.Version),
				evt.EventSourceId,
				evt.Sequence,
				(T)evt.Event
			);
		}

		private void AddEventSource(Guid eventSourceId, Type eventSourceType, long initialVersion)
		{
			EventSource eventSource = new EventSource {
				Id = eventSourceId,
				Type = eventSourceType.ToString(),
				Version = initialVersion
			};

			var coll = database.GetCollection<EventSource>(EVENTSOURCETABLE);
			coll.InsertOne(eventSource);
		}

		private StoredEvent ConvertToStoredEvent(DomainEvent evnt)
		{
			return (StoredEvent)maker.MakeGenericMethod(evnt.Event.GetType()).Invoke(null, new object[] { evnt });
		}

		private void EnsureEventTypes()
		{
			if (!initialized) {
				lock (registerLock) {
					if (!initialized) {
						IClassMapBuilder builder = null;
						try {
							builder = classMapBuilder ?? NcqrsEnvironment.Get<IClassMapBuilder>();
						} catch {
							Log.WarnFormat("Could not find IClassMapBuilder in configuration");
						}

						IEnumerable<BsonClassMap> classMaps = null;

						if (builder == null) {
							var knownEventsEnumerator = NcqrsEnvironment.Get<IKnownEventsEnumerator>();
							if (knownEventsEnumerator == null) {
								throw new NcqrsEnvironmentException("Cannot find IClassMapBuilder or IKnownEventsEnumerator");
							}
							classMaps = knownEventsEnumerator.GetAllEventTypes().Select(x => new BsonClassMap(x));
						} else {
							classMaps = builder.Build();
						}

						foreach (var map in classMaps) {
							if (!BsonClassMap.IsClassMapRegistered(map.ClassType)) {
								BsonClassMap.RegisterClassMap(map);
							}
						}

						initialized = true;
					}
				}
			}
		}

		public long GetNextSequence(string name = DEFAULTSEQUENCE)
		{
			var coll = database.GetCollection<EventSequence>(SEQUENCETABLE);
			var update = Builders<EventSequence>.Update.Inc(x => x.Sequence, 1);
			var options = new FindOneAndUpdateOptions<EventSequence, EventSequence> { ReturnDocument = ReturnDocument.After };

			while (true) {
				try {
					var result = coll.FindOneAndUpdate<EventSequence>(x => x.Name == name, update, options);
					return result.Sequence;
				}
				catch (ConcurrencyException ce) {
					// deliberately do nothing and retry.
				}
			}
		}

		private long? GetVersion(Guid eventSourceId)
		{
			var collection = database.GetCollection<EventSource>(EVENTSOURCETABLE);
			var sort = Builders<EventSource>.Sort.Descending(x => x.Version);

			return collection.Find(x => x.Id == eventSourceId)
				.Sort(sort)
				.Limit(1)
				.Project(x => (int?)x.Version)
				.FirstOrDefault();
		}

		private void SaveEvents(IEnumerable<UncommittedEvent> eventStream)
		{
			Contract.Requires<ArgumentNullException>(eventStream != null, "The argument eventStream could not be null");

			var coll = database.GetCollection<DomainEvent>(EVENTTABLE);
			var eventSourceId = eventStream.First().EventSourceId;

			foreach (var sourcedEvent in eventStream) {
				SaveEvent(sourcedEvent);
			}
		}

		private void SaveEvent(UncommittedEvent uncommittedEvent)
		{
			Contract.Requires<ArgumentNullException>(uncommittedEvent != null, "The argument uncommittedEvent could not be null.");

			var seq = GetNextSequence();

			DomainEvent evnt = new DomainEvent {
				EventSourceId = uncommittedEvent.EventSourceId,
				EventId = uncommittedEvent.EventIdentifier,
				Sequence = uncommittedEvent.EventSequence,
				SequentialId = seq,
				Timestamp = uncommittedEvent.EventTimeStamp,
				Version = uncommittedEvent.EventVersion.ToString(),
				Event= uncommittedEvent.Payload
			};

			var coll = database.GetCollection<DomainEvent>(EVENTTABLE);
			coll.InsertOne(evnt);
		}

		private void SetupCollections()
		{
			var collections = database.ListCollections().ToList().Select(x => x.GetValue("name").AsString).ToList();

			if (!collections.Contains(EVENTTABLE)) {
				Log.InfoFormat("Creating {0} Collection", EVENTTABLE);
				var coll = database.GetCollection<DomainEvent>(EVENTTABLE);
				var indices = Builders<DomainEvent>.IndexKeys.Ascending(x => x.EventSourceId).Ascending(x => x.Version);
				coll.Indexes.CreateOne(indices);

				var byEventId = Builders<DomainEvent>.IndexKeys.Ascending(x => x.EventId);
				coll.Indexes.CreateOne(byEventId);
			}

			if (!collections.Contains(EVENTSOURCETABLE)) {
				Log.InfoFormat("Creating {0} Collection", EVENTSOURCETABLE);
				var coll = database.GetCollection<EventSource>(EVENTSOURCETABLE);
				var indices = Builders<EventSource>.IndexKeys.Ascending(x => x.Id).Descending(x => x.Version);
				coll.Indexes.CreateOne(indices);
			}

			if (!collections.Contains(SNAPSHOTTABLE)) {
				Log.InfoFormat("Creating {0} Collection", SNAPSHOTTABLE);
				var coll = database.GetCollection<Snapshot>(SNAPSHOTTABLE);
				var indices = Builders<Snapshot>.IndexKeys.Ascending(x => x.Id).Descending(x => x.Version);
				coll.Indexes.CreateOne(indices);
			}

			if (!collections.Contains(SEQUENCETABLE)) {
				Log.InfoFormat("Creating {0} Collection", SEQUENCETABLE);
				var coll = database.GetCollection<EventSequence>(SEQUENCETABLE);
				coll.InsertOne(new EventSequence { Sequence = 0, Name = "Default" });
			}
		}

		private void StoreEventsFromSource(Guid eventSourceId, long eventSourceVersion, IEnumerable<UncommittedEvent> eventStream)
		{
			long? currentVersion = GetVersion(eventSourceId);
			long initialVersion = eventStream.First().InitialVersionOfEventSource;

			if (currentVersion == null) {
				AddEventSource(eventSourceId, typeof(object), eventSourceVersion);
			} else if (currentVersion.Value != initialVersion) {
				throw new ConcurrencyException(eventSourceId, eventSourceVersion);
			} else {
				UpdateTheVersionOfTheEventStore(eventSourceId, eventSourceVersion, initialVersion);
			}

			SaveEvents(eventStream);
		}

		private void StoreMultipleSources(UncommittedEventStream eventStream)
		{
			var sources = from evnt in eventStream
						  group evnt by evnt.EventSourceId into eventSourceGroup
						  select eventSourceGroup;

			foreach (var sourceStream in sources) {
				var firstEvent = sourceStream.First();
				var newVersion = firstEvent.InitialVersionOfEventSource + sourceStream.Count();
				StoreEventsFromSource(firstEvent.EventSourceId, newVersion, sourceStream);
			}
		}

		private bool UpdateEventSourceVersion(Guid eventSourceId, long newVersion, long initialVersion)
		{
			var coll = database.GetCollection<EventSource>(EVENTSOURCETABLE);

			var filter = Builders<EventSource>.Filter.And(
				Builders<EventSource>.Filter.Eq(x => x.Id, eventSourceId),
				Builders<EventSource>.Filter.Eq(x => x.Version, initialVersion)
			);

			var update = Builders<EventSource>.Update.Set(x => x.Version, newVersion);

			var result = coll.UpdateOne(filter, update);

			return result.IsAcknowledged ? result.ModifiedCount > 0 : false;
		}

		private void UpdateTheVersionOfTheEventStore(Guid eventSourceId, long eventSourceVersion, long initialVersion)
		{
			bool updated = UpdateEventSourceVersion(eventSourceId, eventSourceVersion, initialVersion);

			if (!updated) {
				throw new ConcurrencyException(eventSourceId, eventSourceVersion);
			}
		}
	}
}