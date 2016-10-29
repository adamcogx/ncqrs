using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Autofac;
using Ncqrs.Domain.Storage;

namespace Ncqrs.Config.Autofac
{
	public class AutofacConfiguration : IEnvironmentConfiguration
	{
		private readonly IContainer container;

		public AutofacConfiguration(Action<ContainerBuilder> setup)
		{
			ContainerBuilder builder = new ContainerBuilder();

			builder.RegisterType<AutofacAggregateRootCreationStrategy>()
				.As<IAggregateRootCreationStrategy>();

			setup(builder);

			this.container = builder.Build();
		}

		public AutofacConfiguration(IContainer container)
		{
			ContainerBuilder builder = new ContainerBuilder();

			builder.RegisterType<AutofacAggregateRootCreationStrategy>()
				.As<IAggregateRootCreationStrategy>();

			builder.Update(container);

			this.container = container;
		}

		public bool TryGet<T>(out T result) where T : class
		{
			return container.TryResolve<T>(out result);
		}
	}
}
