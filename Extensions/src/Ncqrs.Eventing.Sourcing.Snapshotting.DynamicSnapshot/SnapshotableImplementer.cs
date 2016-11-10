using System;
using System.Reflection;
using Ncqrs.Domain;
using System.Linq;
using System.Runtime.Serialization;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections;

namespace Ncqrs.Eventing.Sourcing.Snapshotting.DynamicSnapshot
{
	/// <summary>
	/// Implements <see cref="ISnapshotable&lt;TSnapshot&gt;"/> interface.
	/// </summary>
	/// <typeparam name="TSnapshot">The type of the snapshot.</typeparam>
	internal class SnapshotableImplementer<TSnapshot> : ISnapshotableImplementer<TSnapshot>
			where TSnapshot : DynamicSnapshotBase
	{
		public object Proxy
		{
			private get; set;
		}

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
			if (snapshot == null)
				throw new ArgumentNullException("snapshot");
			if (aggregate == null)
				throw new ArgumentNullException("source");

			InternalTransfer(snapshot, aggregate, direction);
		}

		private void InternalTransfer(object snapshot, object target, TransferDirection direction)
		{
			var aggregateFieldMap = SnapshotableField.GetMap(target.GetType());
			var snapshotFields = snapshot.GetType().GetFields(BindingFlags.Instance | BindingFlags.Public);

			Action<object, FieldInfo, object, FieldInfo> doTransfer = null;

			if (direction == TransferDirection.ToAggregateRoot) {
				doTransfer = (source, sourceField, destination, destinationField)
					=> destinationField.SetValue(destination, TranslateTo(target, destinationField, source, sourceField));
			} else {
				doTransfer = (destination, destinationField, source, sourceField)
					=> destinationField.SetValue(destination, TranslateFrom(target, destinationField, source, sourceField));
			}

			foreach (var snapshotField in snapshotFields) {
				FieldInfo aggregateField;
				if (aggregateFieldMap.TryGetValue(snapshotField.Name, out aggregateField)) {

					// TODO:  If the fields are:
					// ICollection - need to transfer record at a time.
					// IDictionary - need to transfer one at a time (but it different than ICollection... maybe)
					// Entity - this is a special case coming and going
					// Complex object - we need to transfer field by field
					doTransfer(snapshot, snapshotField, target, aggregateField);
				} else {
					// TODO: No field found; throw?
				}
			}
		}

		private object TranslateFrom(object snapshot, FieldInfo snapshotField, object source, FieldInfo aggrgateField)
		{
			var activator = activators.GetOrAdd(new Tuple<Type, Type>(aggrgateField.FieldType, snapshotField.FieldType), (key) => {
				if (!key.Item1.RequiresCustomSnapshotting() && !key.Item2.RequiresCustomSnapshotting()) {
					return (aggRoot, src) => src;
				}

				if (key.Item2.RequiresCustomSnapshotting()) {
					if (key.Item2.IsOfType(typeof(ICollection<>))) {
						if (key.Item1 == key.Item2) {
							return (aggRoot, src) => src;
						}

						var destElementType = GetCollectionElementType(key.Item1);
						var srcElementType = GetCollectionElementType(key.Item2);

						if (destElementType != null) {
							return (aggRoot, src) => {
								var collectionBuilder = collectionActivator.MakeGenericMethod(srcElementType, destElementType);
								var target = Activator.CreateInstance(key.Item2);
								return collectionBuilder.Invoke(this, new object[] { aggRoot, target, src });
							};
						}
					}
				}

				return (aggRoot, src) => null;
			});

			var value = aggrgateField.GetValue(source);
			return activator(snapshot, value);
		}

		private static ConcurrentDictionary<Tuple<Type, Type>, Func<object, object, object>> activators = new ConcurrentDictionary<Tuple<Type, Type>, Func<object, object, object>>();

		private object TranslateTo(object target, FieldInfo destinationField, object source, FieldInfo sourceField)
		{
			// key.Item1 is aggregate, key.Item2 is snapshot

			var activator = activators.GetOrAdd(new Tuple<Type, Type>(sourceField.FieldType, destinationField.FieldType), (key) => {
				if (!key.Item1.RequiresCustomSnapshotting() && !key.Item2.RequiresCustomSnapshotting()) {
					return (aggRoot, src) => src;
				}

				// TODO support Entities

				if (key.Item1.RequiresCustomSnapshotting()) {
					if (key.Item1.HasParameterlessConstructor()) {
						return (aggRoot, src) => {
							var temp = Activator.CreateInstance(key.Item1);
							InternalTransfer(src, temp, TransferDirection.ToAggregateRoot);
							return temp;
						};
					} else {
						return (aggRoot, src) => {
							var temp = FormatterServices.GetUninitializedObject(key.Item1);
							InternalTransfer(src, temp, TransferDirection.ToAggregateRoot);
							return temp;
						};
					}
				}

				return (aggRoot, src) => null;
			});

			var value = sourceField.GetValue(source);
			return activator(target, value);
		}

		private static MethodInfo collectionActivator = typeof(SnapshotableImplementer<TSnapshot>).GetMethod("InternalGenerateCollection", BindingFlags.NonPublic | BindingFlags.Instance);

		private object GenerateCollection(AggregateRoot aggRoot, Type targetType, object value, Type sourceType)
		{
			if (sourceType == targetType) {
				return value;
			}

			var destElementType = GetCollectionElementType(targetType);
			var srcElementType = GetCollectionElementType(sourceType);

			if (destElementType != null) {
				var activator = collectionActivator.MakeGenericMethod(srcElementType, destElementType);
				var target = Activator.CreateInstance(targetType);
				return activator.Invoke(this, new object[] { aggRoot, target, value });
			}

			return null;
		}

		private static Type GetCollectionElementType(Type targetType)
		{
			Type elementType = null;

			foreach (var intf in targetType.GetInterfaces()) {
				if (intf.IsGenericType) {
					var def = intf.GetGenericTypeDefinition();
					if (def == typeof(ICollection<>)) {
						elementType = intf.GetGenericArguments()[0];
					}
				}
			}

			return elementType;
		}

		private object InternalGenerateCollection<TSrc, TDest>(AggregateRoot root, ICollection<TDest> target, ICollection<TSrc> source) where TDest: class, new()
		{
			foreach (var item in source) {

				InternalTransfer(item, )
			}

			return target;
		}

		//TODO:  We need to change this to:
		// ConcurrentDictionary<Tuple<Type, Type>, Func<object, object, object>>
		// The Tuple is Source and Target types.  As we encounter combinations of src/target, we will cache them.
		// Eventually we will generate a custom assembly that can be added to a project.

		//private object GenerateEntity(AggregateRoot aggregate, Type entityType, object value)
		//{
		//	var activator = entityGenerator.GetOrAdd(new Tuple<Type,Type>(entityType, (e) => {
		//		var entityBaseType = e.BaseType;
		//		while (entityBaseType != null) {
		//			if (entityBaseType.IsGenericType) {
		//				var entityBaseTypeDef = entityBaseType.GetGenericTypeDefinition();
		//				if (entityBaseTypeDef == typeof(Entity<>)) {
		//					break;
		//				}
		//			}
		//			entityBaseType = entityBaseType.BaseType;
		//		}

		//		var aggregateField = entityBaseType.GetField("_parent", BindingFlags.NonPublic | BindingFlags.Instance);

		//		Func<object, object, object> creator = (object agg, object snapshot) => {
		//			var entity = FormatterServices.GetUninitializedObject(entityType);
		//			aggregateField.SetValue(value, agg);

		//			InternalTransfer(snapshot, entity, TransferDirection.ToAggregateRoot);
		//			return entity;
		//		};

		//		return creator;
		//	});

		//	return activator(aggregate, value);
		//}
	}
}
