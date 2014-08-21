using Ncqrs.Commanding;
using Ncqrs.Commanding.CommandExecution.Mapping.Attributes;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Commands
{
    [MapsToAggregateRootMethod("MyProject.Domain.Note, Domain", "AddReviewer")]
    public class AddNoteReviewer : CommandBase
    {
        [AggregateRootId]
        public Guid NoteId
        {
            get;
            set;
        }

        public String Name
        {
            get;
            set;
        }

        public Guid ReviewerId { get; set; }
    }
}
