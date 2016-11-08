using System;
using Ncqrs.Commanding;
using Ncqrs.Domain;
using Ncqrs.Domain.Storage;

namespace Ncqrs.Eventing.Sourcing.Snapshotting.DynamicSnapshot
{
    /// <summary>
    /// Represents snapshotable aggregate root.
    /// </summary>
    public class DynamicSnapshotAggregateRootCreationStrategy : SimpleAggregateRootCreationStrategy
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
        /// <param name="aggregateRootType">Type of the aggregate root.</param>
        /// <returns></returns>
        public override AggregateRoot CreateAggregateRoot(Type aggregateRootType, Guid? id)
        {
			// We deliberately don't do anything with the id, as it will be taken care of by the Snapshot.
            return _factory.Create(aggregateRootType);
        }

		public override AggregateRoot CreateAggregateRoot(Type aggregateRootType)
		{
			return _factory.Create(aggregateRootType);
		}
	}
}
