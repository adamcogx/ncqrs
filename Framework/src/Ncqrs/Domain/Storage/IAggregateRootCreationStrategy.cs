using Ncqrs.Commanding;
using System;

namespace Ncqrs.Domain.Storage
{
    /// <summary>
    /// Create AggregateRoots for the domain repository, it will then replay events into them
    /// to re-construct their current state.
    /// </summary>
    public interface IAggregateRootCreationStrategy
    {
        AggregateRoot CreateAggregateRoot(Type aggregateRootType, Guid? id = null);
        T CreateAggregateRoot<T>(Guid? id = null) where T : AggregateRoot;
        AggregateRoot CreateAggregateRootFromCommand(Type aggregateRootType, ICommand command);
        T CreateAggregateRootFromCommand<T>(ICommand command) where T : AggregateRoot;
    }
}
