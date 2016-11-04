using Ncqrs.Commanding.CommandExecution.Mapping;
using System;
using System.Reflection;
using System.Linq;
using Ncqrs.Commanding.CommandExecution.Mapping.Attributes;
using System.Collections.Generic;
using Ncqrs.Commanding;

namespace Ncqrs.Domain.Storage
{
    public abstract class AggregateRootCreationStrategy 
        : IAggregateRootCreationStrategy
    {
        public virtual AggregateRoot CreateAggregateRoot(Type aggregateRootType, Guid? id)
        {
            if (!aggregateRootType.IsSubclassOf(typeof(AggregateRoot)))
            {
                var msg = string.Format("Specified type {0} is not a subclass of AggregateRoot class.", aggregateRootType.FullName);
                throw new ArgumentOutOfRangeException("aggregateRootType", msg);
            }

            return CreateAggregateRootFromType(aggregateRootType, id);
        }

		public virtual AggregateRoot CreateAggregateRoot(Type aggregateRootType)
		{
			if (!aggregateRootType.IsSubclassOf(typeof(AggregateRoot))) {
				var msg = string.Format("Specified type {0} is not a subclass of AggregateRoot class.", aggregateRootType.FullName);
				throw new ArgumentOutOfRangeException("aggregateRootType", msg);
			}

			return CreateAggregateRootFromType(aggregateRootType);
		}

		protected abstract AggregateRoot CreateAggregateRootFromType(Type aggregateRootType, Guid? id);
        protected abstract AggregateRoot CreateAggregateRootFromType(Type aggregateRootType);
        protected abstract AggregateRoot CreateAggregateRootFromTypeAndCommand(Type aggregateRootType, ICommand command);

        public T CreateAggregateRoot<T>(Guid? id) where T : AggregateRoot
        {
            return (T)CreateAggregateRoot(typeof(T), id);
        }

		public T CreateAggregateRoot<T>() where T : AggregateRoot
		{
			return (T)CreateAggregateRoot(typeof(T));
		}

		public virtual AggregateRoot CreateAggregateRootFromCommand(Type aggregateRootType, Commanding.ICommand command)
        {
            if (!aggregateRootType.IsSubclassOf(typeof(AggregateRoot)))
            {
                var msg = string.Format("Specified type {0} is not a subclass of AggregateRoot class.", aggregateRootType.FullName);
                throw new ArgumentOutOfRangeException("aggregateRootType", msg);
            }

            return CreateAggregateRootFromTypeAndCommand(aggregateRootType, command);
        }

        public T CreateAggregateRootFromCommand<T>(Commanding.ICommand command) where T : AggregateRoot
        {
            return (T)CreateAggregateRootFromCommand(typeof(T), command);
        }
    }
}
