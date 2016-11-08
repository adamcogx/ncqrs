using System;
using System.Reflection;
using Ncqrs.Domain;
using System.Linq;

namespace Ncqrs.Eventing.Sourcing.Snapshotting.DynamicSnapshot
{
    /// <summary>
    /// Implements <see cref="ISnapshotable&lt;TSnapshot&gt;"/> interface.
    /// </summary>
    /// <typeparam name="TSnapshot">The type of the snapshot.</typeparam>
    internal class SnapshotableImplementer<TSnapshot> : ISnapshotableImplementer<TSnapshot>
            where TSnapshot : DynamicSnapshotBase
    {
        public object Proxy { private get; set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="SnapshotableImplementer&lt;TSnapshot&gt;"/> class.
        /// </summary>
        public SnapshotableImplementer()
        {

        }

        public TSnapshot CreateSnapshot()
        {
            var snapshot = (TSnapshot)Activator.CreateInstance(typeof(TSnapshot));
            TransferState(snapshot, (AggregateRoot)Proxy, TransferDirection.ToSnapshot);
            return snapshot;
        }

        public void RestoreFromSnapshot(TSnapshot snapshot)
        {
            TransferState(snapshot, (AggregateRoot)Proxy, TransferDirection.ToAggregateRoot);
        }

        private enum TransferDirection
        {
            ToSnapshot,
            ToAggregateRoot
        }

        private void TransferState(DynamicSnapshotBase snapshot, AggregateRoot aggregate, TransferDirection direction)
        {
            if (snapshot == null) throw new ArgumentNullException("snapshot");
            if (aggregate == null) throw new ArgumentNullException("source");

            var aggregateFieldMap = SnapshotableField.GetMap(aggregate.GetType());
            var snapshotFields = snapshot.GetType().GetFields(BindingFlags.Instance | BindingFlags.Public);

            Action<object, FieldInfo, object, FieldInfo> doTransfer = null;

            if (direction == TransferDirection.ToAggregateRoot)
            {
                doTransfer = (source, sourceField, destination, destinationField)
                    => destinationField.SetValue(destination, Translate(aggregate, destinationField, source, sourceField));
            }
            else
            {
                doTransfer = (destination, destinationField, source, sourceField)
                    => destinationField.SetValue(destination, sourceField.GetValue(source));
            }

            foreach (var snapshotField in snapshotFields)
            {
                FieldInfo aggregateField;
                if (aggregateFieldMap.TryGetValue(snapshotField.Name, out aggregateField))
                {

					// TODO:  If the fields are:
					// ICollection - need to transfer record at a time.
					// IDictionary - need to transfer one at a time (but it different than ICollection... maybe)
					// Entity - this is a special case coming and going
					// Complex object - we need to transfer field by field
                    doTransfer(snapshot, snapshotField, aggregate, aggregateField);
                }
                else
                {
                    // TODO: No field found; throw?
                }
            }
        }

		private object Translate(AggregateRoot aggregate, FieldInfo destinationField, object source, FieldInfo sourceField)
		{
			var value = sourceField.GetValue(source);

			if (destinationField.FieldType.IsOfType(typeof(Entity<>)) {
				return GenerateEntity(aggregate, destinationField.FieldType, value);
			}

			if (sourceField.FieldType.IsGenericType) {
				var genDef = sourceField.FieldType.GetGenericTypeDefinition();
			}
		}

		private object GenerateEntity(AggregateRoot aggregate, Type entityType, object value)
		{
			var constructor = FindConstructor(entityType, aggregate);
			if (constructor == null) {
				throw new InvalidOperationException(string.Format("Cannot find constructor for {0}", entityType));
			}

			//TODO:  We need to create a way for us to extract the id from the value for the entity
			return constructor(aggregate, value;
		}

		private static readonly Type aggregateType = typeof(AggregateRoot);

		private Func<AggregateRoot, Guid, object> FindConstructor(Type entityType, AggregateRoot aggregate)
		{
			var constructor = entityType.GetConstructors().FirstOrDefault(IsEntityConstructor);
			int aggIndex, idIndex;

			var parameters = constructor.GetParameters();

			for (int i = 0; i < parameters.Length; i++) {
				var pType = parameters[i].ParameterType;
				if (pType.IsOfType(aggregateType)) {
					aggIndex = i;
				}
				if (pType == typeof(Guid) && pType.Name.Equals("id")) {
					idIndex = i;
				}
			}


		}

		private bool IsEntityConstructor(ConstructorInfo constructor)
		{
			var parms = constructor.GetParameters();
			return parms.Any(p => p.ParameterType.IsOfType(typeof(AggregateRoot)))
				&& parms.Any(p => p.ParameterType == typeof(Guid))
				&& parms.Length == 2;
		}

		private object Translate(AggregateRoot root, FieldInfo aggregateField, object value)
		{
			var type = value.GetType();
			if (type.IsGenericType) {
				var genDef = value
			}
			return value;
		}
	}
}
