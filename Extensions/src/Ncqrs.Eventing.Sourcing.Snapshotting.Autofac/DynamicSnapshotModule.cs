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
	public class DynamicSnapshotModule : Autofac.Module
	{
		private readonly Assembly _assemblyWithAggreagateRoots;

		private readonly bool _generateDynamicSnapshotAssembly;
		private readonly Func<string> assemblyFileName;

		public DynamicSnapshotModule(string assemblyName) : this(Assembly.Load(assemblyName))
		{
		}

		public DynamicSnapshotModule(string assemblyName, bool generateDynamicSnapshotAssembly) : this(Assembly.Load(assemblyName), generateDynamicSnapshotAssembly, null)
		{
		}

		public DynamicSnapshotModule(Assembly assemblyWithAggregateRoots) : this(assemblyWithAggregateRoots, true, null)
		{
		}

		public DynamicSnapshotModule(Assembly assemblyWithAggregateRoots, bool generateDynamicSnapshotAssembly, Func<string> assemblyFilename = null) : base()
		{
			this._generateDynamicSnapshotAssembly = generateDynamicSnapshotAssembly;
			this._assemblyWithAggreagateRoots = assemblyWithAggregateRoots;
			this.assemblyFileName = assemblyFilename;
		}

		protected override void Load(ContainerBuilder builder)
		{
			base.Load(builder);
			builder.RegisterType<DynamicSnapshotAggregateRootCreationStrategy>().As<IAggregateRootCreationStrategy>();
			builder.RegisterType<AggregateSupportsDynamicSnapshotValidator>().As<IAggregateSupportsSnapshotValidator>();
			builder.RegisterType<AggregateDynamicSnapshotter>().As<IAggregateSnapshotter>();
			builder.RegisterType<DynamicSnapshotAssembly>().OnActivating(x => {
				if (this._generateDynamicSnapshotAssembly) {
					x.Instance.CreateAssemblyFrom(this._assemblyWithAggreagateRoots);
				}
			}).As<IDynamicSnapshotAssembly>().SingleInstance();
			builder.RegisterType<SnapshotableAggregateRootFactory>().As<SnapshotableAggregateRootFactory>();
			builder.RegisterType<DynamicSnapshotAssemblyBuilder>().SingleInstance()
				.WithParameter(new NamedParameter("assemblyFilename", assemblyFileName()))
				.As<DynamicSnapshotAssemblyBuilder>();
			builder.RegisterType<DynamicSnapshotTypeBuilder>().As<DynamicSnapshotTypeBuilder>();
			builder.RegisterType<SnapshotableImplementerFactory>().As<SnapshotableImplementerFactory>();
		}
	}
}
