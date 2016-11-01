using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Autofac;
using Ncqrs.Domain;

namespace Ncqrs.Eventing.Sourcing.Snapshotting.DynamicSnapshot
{
    public static class DynamicSnapshotAutofacExtension
    {
        public static void RegisterSnapshotable<T>(this ContainerBuilder self)
        where T : AggregateRoot
        {
            self.Register<T>(context => (T)context.Resolve<SnapshotableAggregateRootFactory>().Create(typeof(T)));
        }
    }
}
