using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace Ncqrs.Eventing.Storage.MongoDB
{
    internal class Snapshot
    {
		public ObjectId	Id
		{
			get; set;
		}

		public Guid EventSourceId
		{
			get; set;
		}

		/// <summary>
		/// Gets the position at which the snapshot applies.
		/// </summary>
		public long Version
		{
			get; set;
		}

		/// <summary>
		/// Gets the snapshot or materialized view of the stream at the revision indicated.
		/// </summary>
		public byte[] Payload
		{
			get; set;
		}
	}
}
