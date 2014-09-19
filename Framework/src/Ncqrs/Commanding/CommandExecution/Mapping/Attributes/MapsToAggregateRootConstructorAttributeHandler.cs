using System;
using System.Linq;
using System.Reflection;
using Ncqrs.Domain;
using Ncqrs.Domain.Storage;

namespace Ncqrs.Commanding.CommandExecution.Mapping.Attributes
{
    public class MapsToAggregateRootConstructorAttributeHandler : IMappingAttributeHandler<MapsToAggregateRootConstructorAttribute>
    {
        private IAggregateRootCreationStrategy _creationStrategy;

        public void Map(MapsToAggregateRootConstructorAttribute attribute, ICommand command, IMappedCommandExecutor executor)
        {
            var commandType = command.GetType();
            ValidateCommandType(commandType);

            Func<ICommand, AggregateRoot> create = (c) =>
            {
                return CreationStrategy.CreateAggregateRootFromCommand(attribute.Type, c);
            };

            Action executorAction = () => executor.ExecuteActionCreatingNewInstance(create);

            if (commandType.IsDefined(typeof(TransactionalAttribute), false))
            {
                var transactionService = NcqrsEnvironment.Get<ITransactionService>();
                transactionService.ExecuteInTransaction(executorAction);
            }
            else
            {
                executorAction();
            }
        }

        protected IAggregateRootCreationStrategy CreationStrategy {
            get
            {
                if (this._creationStrategy == null)
                {
                    _creationStrategy = NcqrsEnvironment.Get<IAggregateRootCreationStrategy>();
                }

                return _creationStrategy;
            }
        }

        private static void ValidateCommandType(Type mappedCommandType)
        {
            var expectedAttribute = typeof (MapsToAggregateRootConstructorAttribute);
            bool containsThisAttribute = mappedCommandType.IsDefined(expectedAttribute, false);

            if (!containsThisAttribute) throw new ArgumentException("The given command type does not contain " +
                                                                    expectedAttribute.FullName + ".",
                                                                    "mappedCommandType");
        }
    }
}