using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using MyNotes.Events;

namespace MyNotes.Domain
{
	public class Comment : Ncqrs.Domain.EntityMappedByConvention<Note>
	{
		public Comment(Note parent, Guid entityId, string comment) : base(parent, entityId)
		{
			Text = comment;
		}

		public string Text
		{
			get;
			private set;
		}
	}
}
