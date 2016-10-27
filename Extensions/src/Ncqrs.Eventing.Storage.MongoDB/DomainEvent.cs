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
		[BsonId]
		public long SequentialId
		{
			get;
			set;
		}

		[BsonElement("eid")]
		public Guid EventId
		{
			get; set;
		}

		[BsonElement("ts")]
		public DateTime Timestamp
		{
			get; set;
		}

		[BsonElement("n")]
		public string Name
		{
			get; set;
		}

		[BsonElement("v")]
		public string Version
		{
			get; set;
		}

		[BsonElement("esid")]
		public Guid EventSourceId
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
