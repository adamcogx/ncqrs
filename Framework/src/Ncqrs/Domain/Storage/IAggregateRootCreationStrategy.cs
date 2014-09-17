using Ncqrs.Commanding;
using System;

namespace Ncqrs.Domain.Storage
{
    public interface IAggregateRootCreationStrategy
    {
        AggregateRoot CreateAggregateRoot(Type aggregateRootType);
        T CreateAggregateRoot<T>() where T : AggregateRoot;
        AggregateRoot CreateAggregateRootFromCommand(Type aggregateRootType, ICommand command);
        T CreateAggregateRootFromCommand<T>(ICommand command) where T : AggregateRoot;
    }
}
