using System;
using Ncqrs.Domain;

namespace Ncqrs.Eventing.Sourcing.Snapshotting.DynamicSnapshot
{
    public static class AggregateExtensions
    {
        public static bool RestoreFromSnapshot(this AggregateRoot aggregateRoot, object snapshot)
        {
            return SnapshotRestorerFactory.Create(aggregateRoot, snapshot).Restore();
        }

        public static bool IsOfType(this Type type, Type target)
        {
            while (type != null) {
                if (type.IsGenericType) {
                    var genDef = type.GetGenericTypeDefinition();
                    if (genDef == target) {
                        return true;
                    }
                }
                type = type.BaseType;
            }

            return false;
        }
    }
}
