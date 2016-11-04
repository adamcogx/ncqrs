using Ncqrs.Commanding;
using System;

namespace Ncqrs.Domain.Storage
{
    public class DelegateAggregateRootCreationStrategy
        : AggregateRootCreationStrategy
    {
        private readonly Func<Type, Guid?, AggregateRoot> _factoryMethod;
        private Func<Type, ICommand, AggregateRoot> _createFromCommandFactoryMethod;

        public DelegateAggregateRootCreationStrategy(Func<Type, Guid?, AggregateRoot> factoryMethod, Func<Type, ICommand, AggregateRoot> createFromCommandFactoryMethod)
        {
            _factoryMethod = factoryMethod;
            _createFromCommandFactoryMethod = createFromCommandFactoryMethod;
        }

        protected override AggregateRoot CreateAggregateRootFromType(Type aggregateRootType, Guid? id)
        {
            return _factoryMethod(aggregateRootType, id);
        }

		protected override AggregateRoot CreateAggregateRootFromType(Type aggregateRootType)
		{
			return _factoryMethod(aggregateRootType, null);
		}

		protected override AggregateRoot CreateAggregateRootFromTypeAndCommand(Type aggregateRootType, ICommand command)
        {
            return _createFromCommandFactoryMethod(aggregateRootType, command);
        }
    }
}
