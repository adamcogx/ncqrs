using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Events
{
    [Serializable]
    public class NoteReviewerAdded
    {
        public string Name { get; set; }

        public Guid ReviewerId { get; set; }
    }
}
