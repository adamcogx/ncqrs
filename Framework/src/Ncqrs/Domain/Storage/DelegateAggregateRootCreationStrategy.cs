using Ncqrs.Commanding;
using System;

namespace Ncqrs.Domain.Storage
{
    public class DelegateAggregateRootCreationStrategy
        : AggregateRootCreationStrategy
    {
        private readonly Func<Type, AggregateRoot> _factoryMethod;
        private Func<Type, ICommand, AggregateRoot> _createFromCommandFactoryMethod;

        public DelegateAggregateRootCreationStrategy(Func<Type, AggregateRoot> factoryMethod, Func<Type, ICommand, AggregateRoot> createFromCommandFactoryMethod)
        {
            _factoryMethod = factoryMethod;
            _createFromCommandFactoryMethod = createFromCommandFactoryMethod;
        }

        protected override AggregateRoot CreateAggregateRootFromType(Type aggregateRootType)
        {
            return _factoryMethod(aggregateRootType);
        }

        protected override AggregateRoot CreateAggregateRootFromTypeAndCommand(Type aggregateRootType, ICommand command)
        {
            return _createFromCommandFactoryMethod(aggregateRootType, command);
        }
    }
}
