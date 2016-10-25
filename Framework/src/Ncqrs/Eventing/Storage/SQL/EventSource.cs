using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ncqrs.Eventing.Storage.SQL
{
    public class EventSource
    {
        public Guid Id
        {
            get; set;
        }

        public string Type
        {
            get; set;
        }

        public long Version
        {
            get; set;
        }
    }
}
