using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace Ncqrs.Eventing.Storage.MongoDB
{
	internal class DomainEvent
	{
		public ObjectId Id
		{
			get;
			set;
		}

		[BsonElement("aid")]
		public Guid AggregateId
		{
			get; set;
		}

		[BsonElement("events")]
		public EventInfo[] Events
		{
			get; set;
		}

		[BsonElement("v")]
		public int Version
		{
			get; set;
		}
	}

	internal class EventInfo
	{
		public EventInfo(UncommittedEvent uncomittedEvent)
		{
			this.Timestamp = uncomittedEvent.EventTimeStamp;
			this.EventId = uncomittedEvent.EventIdentifier;
			this.Version = uncomittedEvent.EventVersion.ToString();
			this.Sequence = uncomittedEvent.EventSequence;
			this.Event = uncomittedEvent.Payload;
		}

		[BsonElement("ts")]
		public DateTime Timestamp
		{
			get; set;
		}

		[BsonElement("eid")]
		public Guid EventId
		{
			get; set;
		}

		[BsonElement("v")]
		public string Version
		{
			get; set;
		}

		[BsonElement("seq")]
		public long Sequence
		{
			get; set;
		}

		[BsonElement("event")]
		public object Event
		{
			get; set;
		}
	}
}
