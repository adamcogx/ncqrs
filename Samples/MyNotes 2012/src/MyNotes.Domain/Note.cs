using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Ncqrs;
using Ncqrs.Domain;
using Ncqrs.Eventing.Sourcing.Snapshotting;
using Ncqrs.Eventing.Sourcing.Snapshotting.DynamicSnapshot;

using MyNotes.Events;

namespace MyNotes.Domain
{
    [DynamicSnapshot]
    public class Note : AggregateRootMappedByConvention
    {
        private string text;
        private DateTime creationDate;

        public Note() { }
        public Note(Guid id, string text) : base(id)
        {
            var clock = NcqrsEnvironment.Get<IClock>();

            this.ApplyEvent(new NoteAdded
            {
                Id = id,
                Text = text,
                CreationDate = clock.UtcNow()
            });
        }

        public void ChangeText(string text)
        {
            this.ApplyEvent(new NoteTextChanged
            {
                Text = text
            });
        }

		public void AddComment(Guid entityId, string comment)
		{
			ApplyEvent(new CommentAdded { Comment = comment, EntityId = entityId });
		}

        protected void OnNewNoteAdded(NoteAdded e)
        {
            this.text = e.Text;
            this.creationDate = e.CreationDate;
        }

        protected void OnNoteTextChanged(NoteTextChanged e)
        {
            this.text = e.Text;
        }

		private Dictionary<Guid, Comment> comments = new Dictionary<Guid, Comment>();

		protected void OnCommentAdded(CommentAdded e)
		{
			var comment = new Comment(this, e.EntityId, e.Comment);
			comments.Add(e.EntityId, comment);
		}
    }

	[Serializable]
	public class CommentHolder
	{
		public CommentHolder(string message)
		{
			this.Message = message;
		}

		public string Message
		{
			get; private set;
		}
	}
}
