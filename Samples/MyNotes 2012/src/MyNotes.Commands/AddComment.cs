using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;
using Ncqrs.Commanding;
using Ncqrs.Commanding.CommandExecution.Mapping.Attributes;

namespace MyNotes.Commands
{
	[MapsToAggregateRootMethod("MyNotes.Domain.Note, MyNotes.Domain", "AddComment")]
	[DataContract]
	public class AddComment : CommandBase
	{
		public AddComment(Guid id, Guid commentId, string comment)
		{
			this.Id = id;
			this.EntityId = commentId;
			this.Comment = comment;
		}

		[AggregateRootId]
		[DataMember]
		public Guid Id
		{
			get; set;
		}

		[DataMember]
		public Guid EntityId
		{
			get; set;
		}

		[DataMember]
		public string Comment
		{
			get; set;
		}
	}
}
