using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Autofac;
using Autofac.Core;
using Ncqrs.Commanding;
using Ncqrs.Domain;
using Ncqrs.Domain.Storage;
using Autofac.Builder;

namespace Ncqrs.Config.Autofac
{
	class AutofacAggregateRootCreationStrategy : SimpleAggregateRootCreationStrategy
	{
		private readonly IComponentContext container;

		public AutofacAggregateRootCreationStrategy(global::Autofac.IComponentContext container)
		{
			this.container = container;
		}

		protected override AggregateRoot CreateAggregateRootFromType(Type aggregateRootType, Guid? id = null)
		{
			object root;

			if (!container.IsRegistered(aggregateRootType)) {
				ContainerBuilder builder = new ContainerBuilder();
				var registration = builder.RegisterType(aggregateRootType);
				builder.Update(container.ComponentRegistry);
			}

			if (id.HasValue) {
				return (AggregateRoot)container.Resolve(aggregateRootType, new NamedParameter("id", id.Value));
			}

			if (container.TryResolve(aggregateRootType, out root)) {
				return (AggregateRoot)root;
			} else {
				return ResolveAggregateRoot(aggregateRootType, Enumerable.Empty<Parameter>());
			}
		}

		protected override AggregateRoot CreateAggregateRootFromTypeAndCommand(Type aggregateRootType, ICommand command)
		{
			var cached = GetCachedCommandConstructorMapping(aggregateRootType, command);
			var parameters = cached.Item2.Select((prop, index) => new NamedParameter(prop.Name.CamelCase(), prop.GetValue(command)));
			if (container.IsRegistered(aggregateRootType)) {
				return (AggregateRoot)container.Resolve(aggregateRootType, parameters);
			} else {
				return (AggregateRoot)ResolveAggregateRoot(aggregateRootType, parameters);
			}
		}

		public AggregateRoot ResolveAggregateRoot(Type aggRootType, IEnumerable<Parameter> parameters)
		{
			var scope = container.Resolve<ILifetimeScope>();
			using (var innerScope = scope.BeginLifetimeScope(b => b.RegisterType(aggRootType))) {
				IComponentRegistration reg;
				innerScope.ComponentRegistry.TryGetRegistration(new TypedService(aggRootType), out reg);

				return (AggregateRoot)container.ResolveComponent(reg, parameters);
			}
		}
	}

	static class StringExtensions {
		public static string CamelCase(this string src)
		{
			return src.Substring(0, 1).ToLower() + src.Substring(1);
		}
	}
}
