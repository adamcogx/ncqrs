using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Autofac;
using Autofac.Core;
using Ncqrs.Domain.Storage;

namespace Ncqrs.Eventing.Sourcing.Snapshotting.DynamicSnapshot
{
	class DynamicSnapshotModule : Autofac.Module
	{
		private readonly Assembly _assemblyWithAggreagateRoots;

		private readonly bool _generateDynamicSnapshotAssembly;
		private readonly Func<string> path;

		public DynamicSnapshotModule(string assemblyName) : this(Assembly.Load(assemblyName))
		{
		}

		public DynamicSnapshotModule(string assemblyName, bool generateDynamicSnapshotAssembly) : this(Assembly.Load(assemblyName), generateDynamicSnapshotAssembly, null)
		{
		}

		public DynamicSnapshotModule(Assembly assemblyWithAggregateRoots) : this(assemblyWithAggregateRoots, true, null)
		{
		}

		public DynamicSnapshotModule(Assembly assemblyWithAggregateRoots, bool generateDynamicSnapshotAssembly, Func<string> path = null) : base()
		{
			this._generateDynamicSnapshotAssembly = generateDynamicSnapshotAssembly;
			this._assemblyWithAggreagateRoots = assemblyWithAggregateRoots;
			this.path = path;
		}

		protected override void Load(ContainerBuilder builder)
		{
			base.Load(builder);
			builder.RegisterType<DynamicSnapshotAggregateRootCreationStrategy>().As<IAggregateRootCreationStrategy>();
			builder.RegisterType<AggregateSupportsDynamicSnapshotValidator>().As<IAggregateSupportsSnapshotValidator>();
			builder.RegisterType<AggregateDynamicSnapshotter>().As<IAggregateSnapshotter>();
			builder.RegisterType<DynamicSnapshotAssembly>().OnActivated(x => {
				if (this._generateDynamicSnapshotAssembly) {
					x.Instance.CreateAssemblyFrom(this._assemblyWithAggreagateRoots);
				}
			}).As<IDynamicSnapshotAssembly>();
			builder.RegisterType<SnapshotableAggregateRootFactory>().As<SnapshotableAggregateRootFactory>();
			builder.RegisterType<DynamicSnapshotAssemblyBuilder>().SingleInstance()
				.WithParameter(new NamedParameter("pathFactory", path))
				.As<DynamicSnapshotAssemblyBuilder>();
			builder.RegisterType<DynamicSnapshotTypeBuilder>().As<DynamicSnapshotTypeBuilder>();
			builder.RegisterType<SnapshotableImplementerFactory>().As<SnapshotableImplementerFactory>();
		}
	}
}
