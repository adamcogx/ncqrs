using System;
using Ncqrs.Domain;
using System.Linq;

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
			var temp = type;
			while (temp != null) {
				if (temp == target) {
					return true;
				}
				if (temp.IsGenericType) {
					var genDef = temp.GetGenericTypeDefinition();
					if (genDef == target) {
						return true;
					}
				}
				temp = temp.BaseType;
			}

			return type.GetInterfaces().Any(x => x.IsOfType(target));
		}
	}
}
