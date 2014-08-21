using Events;
using MyProject.Domain;
using Ncqrs.Domain;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Domain
{
    [Serializable]
    public class Reviewer : EntityMappedByConvention<Note>
    {
        public Reviewer(Note parent, Guid id, string name) : base(parent, id) 
        {
            this.Name = name;
        }

        public string Name { get; private set; }

        internal void UpdateName(string newName)
        {
            ApplyEvent(new ReviewerNameChanged() { NewName = newName });
        }

        protected void OnReviewerNameChanged(ReviewerNameChanged e)
        {
            this.Name = e.NewName;
        }
    }
}
