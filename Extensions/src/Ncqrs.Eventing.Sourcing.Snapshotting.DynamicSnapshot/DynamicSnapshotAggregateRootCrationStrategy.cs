using System;
using Ncqrs.Commanding;
using Ncqrs.Domain;
using Ncqrs.Domain.Storage;

namespace Ncqrs.Eventing.Sourcing.Snapshotting.DynamicSnapshot
{
    /// <summary>
    /// Represents snapshotable aggregate root.
    /// </summary>
    public class DynamicSnapshotAggregateRootCreationStrategy : IAggregateRootCreationStrategy
    {
        private readonly SnapshotableAggregateRootFactory _factory;

        /// <summary>
        /// Initializes a new instance of the <see cref="DynamicSnapshotAggregateRootCreationStrategy"/> class.
        /// </summary>
        /// <param name="factory">The factory.</param>
        public DynamicSnapshotAggregateRootCreationStrategy(SnapshotableAggregateRootFactory factory)
        {
            _factory = factory;
        }

        /// <summary>
        /// Creates an instance of empty snapshotable aggregate root.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        public T CreateAggregateRoot<T>() where T : AggregateRoot
        {
            return (T)CreateAggregateRoot(typeof(T));
        }

        /// <summary>
        /// Creates an instance of empty snapshotable aggregate root.
        /// </summary>
        /// <param name="aggregateRootType">Type of the aggregate root.</param>
        /// <returns></returns>
        public AggregateRoot CreateAggregateRoot(Type aggregateRootType)
        {
            return _factory.Create(aggregateRootType);
        }

        public AggregateRoot CreateAggregateRootFromCommand(Type aggregateRootType, ICommand command)
        {
            throw new NotImplementedException();
        }

        public T CreateAggregateRootFromCommand<T>(ICommand command) where T : AggregateRoot
        {
            throw new NotImplementedException();
        }
    }
}
