using System;
using Ncqrs.Domain;
using Ncqrs.Domain.Storage;
using Ninject;
using Ninject.Parameters;

namespace Ncqrs.Config.Ninject
{
	public class NinjectAggregateRootCreationStrategy
		: SimpleAggregateRootCreationStrategy
	{

		private readonly IKernel _kernel;

		public NinjectAggregateRootCreationStrategy(IKernel kernel)
		{
			_kernel = kernel;
		}

		protected override AggregateRoot CreateAggregateRootFromType(Type aggregateRootType, Guid? id)
		{
			return (AggregateRoot)_kernel.Get(aggregateRootType, new ConstructorArgument("id", id.Value));
		}

		protected override AggregateRoot CreateAggregateRootFromType(Type aggregateRootType)
		{
			return (AggregateRoot)_kernel.Get(aggregateRootType);
		}

		protected override AggregateRoot CreateAggregateRootFromTypeAndCommand(Type aggregateRootType, Commanding.ICommand command)
		{
			var root = base.CreateAggregateRootFromTypeAndCommand(aggregateRootType, command);
			_kernel.Inject(root);
			return root;
		}
	}
}
