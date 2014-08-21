using Ncqrs.Commanding;
using Ncqrs.Commanding.CommandExecution.Mapping.Attributes;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Commands
{
    [MapsToAggregateRootMethod("MyProject.Domain.Note, Domain", "ChangeReviewerName")]
    public class ChangeNoteReviewerName : CommandBase
    {
        [AggregateRootId]
        public Guid NoteId
        {
            get;
            set;
        }

        public Guid ReviewerId { get; set; }

        public String NewName
        {
            get;
            set;
        }
    }
}
