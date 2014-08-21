using Ncqrs.Domain.Storage;
using StructureMap;
using StructureMap.Configuration.DSL;
using StructureMap.Configuration.DSL.Expressions;
using StructureMap.Pipeline;
using System;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace Ncqrs.Eventing.Sourcing.Snapshotting.DynamicSnapshot
{
	public class DynamicSnapshotRegistry : Registry
	{
		private readonly Assembly _assemblyWithAggreagateRoots;

		private readonly bool _generateDynamicSnapshotAssembly;

		public DynamicSnapshotRegistry(string assemblyName) : this(Assembly.Load(assemblyName))
		{
		}

		public DynamicSnapshotRegistry(string assemblyName, bool generateDynamicSnapshotAssembly) : this(Assembly.Load(assemblyName), generateDynamicSnapshotAssembly, null)
		{
		}

		public DynamicSnapshotRegistry(Assembly assemblyWithAggregateRoots) : this(assemblyWithAggregateRoots, true, null)
		{
		}

		public DynamicSnapshotRegistry(Assembly assemblyWithAggregateRoots, bool generateDynamicSnapshotAssembly, Func<string> path = null)
		{
			this._generateDynamicSnapshotAssembly = generateDynamicSnapshotAssembly;
			this._assemblyWithAggreagateRoots = assemblyWithAggregateRoots;
			this.Init(path);
		}

		protected void Init(Func<string> path)
		{
			base.For<IAggregateRootCreationStrategy>().Use<DynamicSnapshotAggregateRootCreationStrategy>();
			base.For<IAggregateSupportsSnapshotValidator>().Use<AggregateSupportsDynamicSnapshotValidator>();
			base.For<IAggregateSnapshotter>().Use<AggregateDynamicSnapshotter>();
			base.For<IDynamicSnapshotAssembly>().Singleton().Use<DynamicSnapshotAssembly>().OnCreation("Generate Snapshot Assembly", (IContext ctx, DynamicSnapshotAssembly x) => {
				if (this._generateDynamicSnapshotAssembly)
				{
					x.CreateAssemblyFrom(this._assemblyWithAggreagateRoots);
				}
			});
			base.ForConcreteType<SnapshotableAggregateRootFactory>();
			base.For<DynamicSnapshotAssemblyBuilder>().Singleton().Use<DynamicSnapshotAssemblyBuilder>().Ctor<Func<string>>("pathFactory").Is(path);
			base.ForConcreteType<DynamicSnapshotTypeBuilder>();
			base.ForConcreteType<SnapshotableImplementerFactory>();
		}
	}
}