using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Ncqrs.Eventing.Sourcing.Mapping
{
    class MatchedMethods
    {
        public System.Reflection.MethodInfo MethodInfo { get; set; }

        public System.Reflection.ParameterInfo FirstParameter { get; set; }
    }
}
