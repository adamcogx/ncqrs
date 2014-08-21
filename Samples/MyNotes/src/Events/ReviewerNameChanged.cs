using Ncqrs.Eventing.Sourcing;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Events
{
    [Serializable]
    public class ReviewerNameChanged : EntitySourcedEventBase
    {
        public String NewName
        {
            get;
            set;
        }
    }
}
