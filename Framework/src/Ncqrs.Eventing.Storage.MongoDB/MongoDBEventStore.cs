using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization.Formatters.Binary;
using MongoDB.Bson.Serialization;
using MongoDB.Driver;
using Ncqrs.Eventing.Sourcing;
using Ncqrs.Eventing.Sourcing.Snapshotting;

namespace Ncqrs.Eventing.Storage.MongoDB
{
	public class MongoDBEventStore : IEventStore, ISnapshotStore
	{
		public const string DEFAULT_DATABASE = "EventStore";
		public const string DEFAULT_SERVER_URI = "mongodb://127.0.0.1:27017";
		public const string EVENTSOURCETABLE = "EventSources";
		public const string EVENTTABLE = "DomainEvents";
		public const string SNAPSHOTTABLE = "Snapshots";

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
		}

		public CommittedEventStream ReadFrom(Guid id, long minVersion, long maxVersion)
		{
			EnsureEventTypes();
			var events = new List<CommittedEvent>();

			var coll = database.GetCollection<DomainEvent>(EVENTTABLE);
			var filter = Builders<DomainEvent>.Filter.Eq(x => x.AggregateId, id);

			var count = maxVersion - minVersion;
			var start = (int)minVersion;

			if (start < 0) {
				start = 0;
			}

			if (count == -1) {
				count = int.MaxValue;
			}

			var results = coll.Aggregate().Match(filter).Project(x => new {
				Events = x.Events.Skip(start).Take((int)count)
			});

			foreach (var result in results.First().Events) {
				events.Add(ReadEventFromArray(id, result));
			}

			return new CommittedEventStream(id, events);
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

		private static StoredEvent<T> MakeStoredEvent<T>(Guid aggregateId, EventInfo evt, T payload)
		{
			return new StoredEvent<T>(
				evt.EventId,
				evt.Timestamp,
				evt.Event.GetType().Name,
				Version.Parse(evt.Version),
				aggregateId,
				evt.Sequence,
				payload
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

		private StoredEvent ConvertToStoredEvent(Guid aggregateId, EventInfo result)
		{
			return (StoredEvent)maker.MakeGenericMethod(result.Event.GetType()).Invoke(null, new object[] { aggregateId, result, result.Event });
		}

		private void EnsureEventTypes()
		{
			if (!initialized) {
				lock (registerLock) {
					if (!initialized) {
						var builder = classMapBuilder ?? NcqrsEnvironment.Get<IClassMapBuilder>();
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
							BsonClassMap.RegisterClassMap(map);
						}

						initialized = true;
					}
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

		private CommittedEvent ReadEventFromArray(Guid aggregateId, EventInfo result)
		{
			StoredEvent rawEvent = ConvertToStoredEvent(aggregateId, result);

			// TODO: We should be doing:
			// var document = _translator.TranslateToCommon(rawEvent);
			// _converter.Upgrade(document);
			// (See MsSqlServerEventStore's ReadEventFromDbReader)
			//
			// But we only need that once we have a V2 of the application...

			// TODO: Legacy stuff... we do not have a dummy id with the current schema.
			var dummyCommitId = Guid.Empty;
			var evnt = new CommittedEvent(dummyCommitId, rawEvent.EventIdentifier, rawEvent.EventSourceId, rawEvent.EventSequence, rawEvent.EventTimeStamp, result.Event, rawEvent.EventVersion);

			// TODO: Legacy stuff... should move.
			if (evnt is ISourcedEvent) {
				((ISourcedEvent)evnt).InitializeFrom(rawEvent);
			}

			return evnt;
		}

		private void SaveEvents(IEnumerable<UncommittedEvent> eventStream)
		{
			Contract.Requires<ArgumentNullException>(eventStream != null, "The argument eventStream could not be null");

			var coll = database.GetCollection<DomainEvent>(EVENTTABLE);
			var eventSourceId = eventStream.First().EventSourceId;
			var exists = coll.Find(x => x.AggregateId == eventSourceId).Any();

			if (!exists) {
				DomainEvent evnt = new DomainEvent {
					AggregateId = eventSourceId,
					Version = 0,
					Events = new EventInfo[0]
				};

				coll.InsertOne(evnt);
			}

			var filter = Builders<DomainEvent>.Filter.Eq(x => x.AggregateId, eventSourceId);
			var addEventsUpdate = Builders<DomainEvent>.Update.PushEach(x => x.Events, eventStream.Select(x => new EventInfo(x)));
			var versionUpdate = Builders<DomainEvent>.Update.Inc(x => x.Version, eventStream.Count());

			var update = Builders<DomainEvent>.Update.Combine(addEventsUpdate, versionUpdate);

			coll.FindOneAndUpdate(filter, update);
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
	}
}