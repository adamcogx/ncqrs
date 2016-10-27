using System;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Generic;
using FluentAssertions;
using Ncqrs.Eventing;
using Ncqrs.Eventing.Sourcing;
using Ncqrs.Eventing.Sourcing.Snapshotting;
using Ncqrs.Eventing.Storage.SQL;
using Ncqrs.Spec;
using Rhino.Mocks;
using System.Data.SqlClient;
using Ncqrs.Eventing.Storage;
using System.Configuration;
using Xunit;
using MongoDB.Driver;
using Ncqrs.Eventing.Storage.MongoDB;
using MongoDB.Bson.Serialization;
using Xunit.Abstractions;

namespace Ncqrs.Tests.Eventing.Storage.SQL
{
	[Collection("MongoDBEventStore")]
	public class MongoDBEventStoreTests
	{
		[Serializable]
		public class CustomerCreatedEvent
		{
			protected CustomerCreatedEvent()
			{
			}

			public CustomerCreatedEvent(string name, int age)
			{
				Name = name;
				Age = age;
			}

			public string Name
			{
				get; set;
			}

			public int Age
			{
				get;
				set;
			}
		}

		[Serializable]
		public class CustomerNameChanged
		{
			public Guid CustomerId
			{
				get; set;
			}
			public string NewName
			{
				get; set;
			}

			protected CustomerNameChanged()
			{

			}

			public CustomerNameChanged(string newName)
			{
				NewName = newName;
			}
		}

		[Serializable]
		public class AccountNameChangedEvent : IEntitySourcedEvent
		{
			public Guid CustomerId
			{
				get; set;
			}
			public Guid AccountId
			{
				get; set;
			}
			public string NewAccountName
			{
				get; set;
			}

			public AccountNameChangedEvent()
			{

			}

			public AccountNameChangedEvent(Guid accountId, string newAccountName)
			{
				NewAccountName = newAccountName;
				AccountId = accountId;
			}

			public Guid EntityId
			{
				get {
					return AccountId;
				}
			}

			public Guid AggregateId
			{
				get {
					return CustomerId;
				}
			}
		}

		[Serializable]
		public class MySnapshot : Snapshot
		{
		}

		private const string DEFAULT_DATABASE_KEY = "EventStore";
		private readonly string databaseName;
		private readonly ITestOutputHelper output;

		public MongoDBEventStoreTests(ITestOutputHelper output)
		{
			databaseName = ConfigurationManager.AppSettings[DEFAULT_DATABASE_KEY];
			MongoClient client = new MongoClient();
			var db = client.GetDatabase(databaseName);
			db.DropCollection(MongoDBEventStore.EVENTTABLE);
			db.DropCollection(MongoDBEventStore.EVENTSOURCETABLE);
			db.DropCollection(MongoDBEventStore.SNAPSHOTTABLE);

			this.output = output;
		}

		[SkippableFact]
		public void Storing_event_source_should_succeed()
		{
			var targetStore = new MongoDBEventStore(MongoDBEventStore.DEFAULT_SERVER_URI, databaseName, new MockClassMapper());
			var theEventSourceId = Guid.NewGuid();
			var theCommitId = Guid.NewGuid();
			var theCommitId2 = Guid.NewGuid();

			var eventStream = Prepare.Events(
				new CustomerCreatedEvent("Foo", 35),
				new CustomerNameChanged("Name" + 2))
				.ForSourceUncomitted(theEventSourceId, theCommitId);

			var eventStream2 = Prepare.Events(
				new CustomerNameChanged("Name" + 3),
				new CustomerNameChanged("Name" + 4))
				.ForSourceUncomitted(theEventSourceId, theCommitId2, 3);

			targetStore.Store(eventStream);
			targetStore.Store(eventStream2);

			var eventsFromStore = targetStore.ReadFrom(theEventSourceId, long.MinValue, long.MaxValue);

			var totalEvents = eventStream.Union(eventStream2);

			eventsFromStore.Count().Should().Be(totalEvents.Count());

			for (int i = 0; i < totalEvents.Count(); i++) {
				var uncommittedEvent = totalEvents.ElementAt(i);
				var committedEvent = eventsFromStore.ElementAt(i);

				committedEvent.EventSourceId.Should().Be(uncommittedEvent.EventSourceId);
				committedEvent.EventIdentifier.Should().Be(uncommittedEvent.EventIdentifier);
				committedEvent.EventSequence.Should().Be(uncommittedEvent.EventSequence);
				committedEvent.Payload.GetType().Should().Be(uncommittedEvent.Payload.GetType());
			}
		}

		[SkippableFact]
		public void Storing_entity_sourced_event_should_preserve_entity_id()
		{
			var targetStore = new MongoDBEventStore(MongoDBEventStore.DEFAULT_SERVER_URI, databaseName, new MockClassMapper());
			var theEventSourceId = Guid.NewGuid();
			var theCommitId = Guid.NewGuid();

			var theEntityId = Guid.NewGuid();
			var eventStream = Prepare.Events(new AccountNameChangedEvent(theEntityId, "NewName"))
				.ForSourceUncomitted(theEventSourceId, theCommitId);

			targetStore.Store(eventStream);

			var restoredEvent = targetStore.ReadFrom(theEventSourceId, long.MinValue, long.MaxValue).Single();

			var payload = (AccountNameChangedEvent)restoredEvent.Payload;
			payload.EntityId.Should().Be(theEntityId);
		}

		[SkippableFact]
		public void Saving_with_concurrent_event_edits_should_be_subject_to_concurrency_checks()
		{
			// test created in response to an issue with concurrent edits happening within the window between
			// reading the current version number of the aggregate and the event source record being updated with
			// the new version number. this would leave the event stream for an event source out of sequence and
			// the aggregate in a state in which it could not be retrieved :o
			var concurrencyExceptionThrown = false;

			var targetStore = new MongoDBEventStore(MongoDBEventStore.DEFAULT_SERVER_URI, databaseName, new MockClassMapper());

			var theEventSourceId = Guid.NewGuid();
			var theCommitId = Guid.NewGuid();

			// make sure that the event source for the aggregate is created
			var creationEvent = Prepare.Events(new CustomerCreatedEvent("Foo", 35))
				.ForSourceUncomitted(theEventSourceId, theCommitId);
			targetStore.Store(creationEvent);

			var tasks = new Task[130];

			var concurrencyExceptionCount = 0;

			// now simulate concurreny updates coming in on the same aggregate
			for (int idx = 0; idx < tasks.Length; idx++) {
				tasks[idx] = Task.Factory.StartNew(() => {

					var changeEvent = new CustomerNameChanged(DateTime.Now.Ticks.ToString()) { CustomerId = theEventSourceId };
					var eventStream = Prepare.Events(changeEvent)
						.ForSourceUncomitted(theEventSourceId, Guid.NewGuid(), 2);

					try {
						targetStore.Store(eventStream);

					}
					catch (ConcurrencyException) {
						concurrencyExceptionThrown = true;
						concurrencyExceptionCount++;
					}

					targetStore.ReadFrom(theEventSourceId, long.MinValue, long.MaxValue);

				});
			}

			Task.WaitAll(tasks);

			if (concurrencyExceptionThrown == false) {
				Assert.True(false, "We're expecting concurrency exceptions!");
			} else {
				output.WriteLine("Concurrecy Exception Count: {0}", concurrencyExceptionCount);
			}
		}

		[SkippableFact]
		public void Saving_with_concurrent_event_adds_should_not_be_causing_deadlocks()
		{
			var targetStore = new MongoDBEventStore(MongoDBEventStore.DEFAULT_SERVER_URI, databaseName, new MockClassMapper());

			var tasks = new Task[30]; // this number require to reproduce the issue might vary depending on hardware

			for (int idx = 0; idx < tasks.Length; idx++) {
				tasks[idx] = Task.Factory.StartNew(() => {
					var theEventSourceId = Guid.NewGuid();
					var theCommitId = Guid.NewGuid();

					var eventStream = Prepare.Events(new CustomerCreatedEvent(Task.CurrentId.ToString(), 35))
						.ForSourceUncomitted(theEventSourceId, theCommitId);

					// should not be receiving a deadlock
					targetStore.Store(eventStream);
				});
			}

			Task.WaitAll(tasks);
		}

		[SkippableFact]
		public void Saving_snapshot_should_not_throw_an_exception_when_snapshot_is_valid()
		{
			var targetStore = new MongoDBEventStore(MongoDBEventStore.DEFAULT_SERVER_URI, databaseName, new MockClassMapper());

			var anId = Guid.NewGuid();
			var aCommitId = Guid.NewGuid();
			var aVersion = 12;

			var eventStream = Prepare.Events(new object())
			   .ForSourceUncomitted(anId, aCommitId);

			var mySnapshot = new MySnapshot();

			var snapshot = new Ncqrs.Eventing.Sourcing.Snapshotting.Snapshot(anId, aVersion, mySnapshot);


			targetStore.Store(eventStream);
			targetStore.SaveSnapshot(snapshot);

			var savedSnapshot = targetStore.GetSnapshot(anId, long.MaxValue);
			savedSnapshot.EventSourceId.Should().Be(anId);
			savedSnapshot.Version.Should().Be(aVersion);
			savedSnapshot.Payload.Should().NotBeNull();
			savedSnapshot.Payload.Should().BeOfType<MySnapshot>();
		}

		[SkippableFact]
		public void Storing_empty_event_stream_should_not_throw()
		{
			var targetStore = new MongoDBEventStore(MongoDBEventStore.DEFAULT_SERVER_URI, databaseName, new MockClassMapper());

			var theEventSourceId = Guid.NewGuid();
			var theCommitId = Guid.NewGuid();

			var eventStream = Prepare.Events(new object[0])
				.ForSourceUncomitted(theEventSourceId, theCommitId);

			targetStore.Store(eventStream);

			Assert.True(true);
		}

		private class MockClassMapper : IClassMapBuilder
		{
			private List<Type> types = new List<Type> {
				typeof(CustomerCreatedEvent),
				typeof(CustomerNameChanged),
				typeof(AccountNameChangedEvent)
			};

			public IEnumerable<BsonClassMap> Build()
			{
				foreach (var type in types) {
					var map = new BsonClassMap(type);
					map.AutoMap();
					yield return map;
				}
			}
		}
	}
}
