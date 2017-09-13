using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Ncqrs.Eventing.Sourcing;

namespace MyNotes.Events
{
    public class CommentAdded
    {
		public Guid EntityId
		{
			get; set;
		}

		public string Comment
		{
			get; set;
		}
	}
}
