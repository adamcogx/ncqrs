using Ncqrs.Domain;
using StructureMap;
using StructureMap.Configuration.DSL.Expressions;
using StructureMap.Pipeline;
using System;
using System.Runtime.CompilerServices;

namespace Ncqrs.Eventing.Sourcing.Snapshotting.DynamicSnapshot
{
	public static class DynamicSnapshotStructureMapExtension
	{
		public static LambdaInstance<T,T> AsSnapshotable<T>(this CreatePluginFamilyExpression<T> self)
		where T : AggregateRoot
		{
			return self.Use<T>("Create type from Snapshot Factory", context => (T)context.GetInstance<SnapshotableAggregateRootFactory>().Create(typeof(T)));
		}
	}
}