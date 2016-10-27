using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace Ncqrs.Eventing.Storage.MongoDB
{
	public class EventSequence
	{
		public ObjectId Id
		{
			get; set;
		}

		public string Name
		{
			get; set;
		}

		[BsonElement("seq")]
		public long Sequence
		{
			get; set;
		}
	}
}
