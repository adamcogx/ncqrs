using System;
using Ncqrs.Domain;
using System.Collections.Generic;

namespace Ncqrs.Eventing.Sourcing.Snapshotting.DynamicSnapshot
{
    internal static class SnapshotRestorerFactory
    {
        private static Dictionary<Type, Type> restoreCache = new Dictionary<Type, Type>();

        public static ISnapshotRestorer Create(AggregateRoot aggregateRoot, object snapshot)
        {
            var snapshotType = snapshot.GetType();
            if (!restoreCache.ContainsKey(snapshotType))
            {
                if (!typeof(DynamicSnapshotBase).IsAssignableFrom(snapshotType)) throw new ArgumentException("snapshot must inherit DynamicSnapshotBase");
                var restorerType = typeof(SnapshotRestorer<>).MakeGenericType(snapshot.GetType());
                restoreCache[snapshotType] = restorerType;
            }

            return (ISnapshotRestorer)Activator.CreateInstance(restoreCache[snapshotType], aggregateRoot, snapshot);
        }
    }
}
