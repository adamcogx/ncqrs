using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Reflection;
using System.Runtime.Serialization;
using Ncqrs.Domain;

namespace Ncqrs.Eventing.Sourcing.Snapshotting.DynamicSnapshot
{
	/// <summary>
	/// Implements <see cref="ISnapshotable&lt;TSnapshot&gt;"/> interface.
	/// </summary>
	/// <typeparam name="TSnapshot">The type of the snapshot.</typeparam>
	internal class SnapshotableImplementer<TSnapshot> : ISnapshotableImplementer<TSnapshot>
			where TSnapshot : DynamicSnapshotBase
	{
		private static ConcurrentDictionary<Tuple<Type, Type, TransferDirection>, Func<AggregateRoot, object, object>> activators = new ConcurrentDictionary<Tuple<Type, Type, TransferDirection>, Func<AggregateRoot, object, object>>();

		private static MethodInfo collectionActivator = typeof(SnapshotableImplementer<TSnapshot>).GetMethod("InternalGenerateCollection", BindingFlags.NonPublic | BindingFlags.Instance);
		private static MethodInfo dictionaryActivator = typeof(SnapshotableImplementer<TSnapshot>).GetMethod("InternalGenerateDictionary", BindingFlags.NonPublic | BindingFlags.Instance);

		private static Dictionary<Type, Func<Type, Func<AggregateRoot, object, object>>> constructors = new Dictionary<Type, Func<Type, Func<AggregateRoot, object, object>>>();

		static SnapshotableImplementer()
		{
			AddObjectConstructor(typeof(Entity<>), (type) => {
				Type aggRootActualType = GetEntityAggregateRootType(type);
				var fieldName = "Ncqrs.Domain.Entity`1[[" + aggRootActualType.AssemblyQualifiedName + "]]__entityId";
				ConstructorInfo constructor = GetEntityConstructor(type);

				return (root, src) => {
					var entityId = src.GetType().GetField(fieldName, BindingFlags.Public | BindingFlags.Instance).GetValue(src);

					var result = FormatterServices.GetUninitializedObject(type);
					constructor.Invoke(result, new object[] { root, entityId });
					return result;
				};
			});
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="SnapshotableImplementer&lt;TSnapshot&gt;"/> class.
		/// </summary>
		public SnapshotableImplementer()
		{
		}

		private enum TransferDirection
		{
			ToSnapshot,
			ToAggregateRoot
		}

		public object Proxy
		{
			private get; set;
		}

		/// <summary>
		/// Adds an object constructor used to construct objects.
		/// </summary>
		/// <param name="rootType"></param>
		/// <param name="constructor"></param>
		public static void AddObjectConstructor(Type rootType, Func<AggregateRoot, object, object> constructor)
		{
			constructors.Add(rootType, (type) => constructor);
		}

		public static void AddObjectConstructor(Type rootType, Func<Type, Func<AggregateRoot, object, object>> constructorFactory)
		{
			constructors.Add(rootType, constructorFactory);
		}

		public TSnapshot CreateSnapshot()
		{
			var snapshot = Activator.CreateInstance<TSnapshot>();
			TransferState(snapshot, (AggregateRoot)Proxy, TransferDirection.ToSnapshot);
			return snapshot;
		}

		public void RestoreFromSnapshot(TSnapshot snapshot)
		{
			TransferState(snapshot, (AggregateRoot)Proxy, TransferDirection.ToAggregateRoot);
		}

		private static object Activate(Type type)
		{
			if (type.HasParameterlessConstructor()) {
				return Activator.CreateInstance(type);
			} else {
				return FormatterServices.GetUninitializedObject(type);
			}
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

		private static Type GetEntityAggregateRootType<TDest>()
		{
			var temp = typeof(TDest);
			return GetEntityAggregateRootType(temp);
		}

		private static Type GetEntityAggregateRootType(Type entity)
		{
			while (entity != null) {
				if (entity.IsGenericType) {
					var genType = entity.GetGenericTypeDefinition();
					if (genType == typeof(Entity<>)) {
						var genArgs = entity.GetGenericArguments();
						return genArgs[0];
					}
				}

				entity = entity.BaseType;
			}

			return typeof(object);
		}

		private static ConstructorInfo GetEntityConstructor<TDest>()
		{
			var temp = typeof(TDest);
			return GetEntityConstructor(temp);
		}

		private static ConstructorInfo GetEntityConstructor(Type entity)
		{
			while (entity != null) {
				if (entity.IsGenericType) {
					var genDef = entity.GetGenericTypeDefinition();
					var genArgs = entity.GetGenericArguments();

					if (genDef == typeof(EntityMappedByConvention<>)) {
						var ctors = entity.GetConstructors();
						return entity.GetConstructor(BindingFlags.NonPublic | BindingFlags.Instance, null, new Type[] { genArgs[0], typeof(Guid) }, new ParameterModifier[0]);
					}

					if (genDef == typeof(Entity<>)) {
						return entity.GetConstructor(new Type[] { genArgs[0], typeof(Guid) });
					}
				}

				entity = entity.BaseType;
			}

			return null;
		}

		private static Func<AggregateRoot, object, object> GetObjectConstructor(Type objType)
		{
			foreach (var mapping in constructors) {
				if (objType.IsOfType(mapping.Key)) {
					return mapping.Value(objType);
				}
			}

			if (objType.HasParameterlessConstructor()) {
				return (root, src) => Activator.CreateInstance(objType);
			} else {
				return (root, src) => FormatterServices.GetUninitializedObject(objType);
			}
		}

		private object GenerateCollection(AggregateRoot aggRoot, Type targetType, object srcCollection, Type sourceType, TransferDirection direction)
		{
			if (sourceType == targetType) {
				return srcCollection;
			}

			var destElementType = GetCollectionElementType(targetType);
			var srcElementType = GetCollectionElementType(sourceType);

			if (destElementType != null) {
				var activator = collectionActivator.MakeGenericMethod(srcElementType, destElementType);
				var targetCollection = Activate(targetType);
				return activator.Invoke(this, new object[] { aggRoot, targetCollection, srcCollection, direction });
			}

			return null;
		}

		private object GenerateDictionary(AggregateRoot aggRoot, Type targetType, object value, Type sourceType, TransferDirection direction)
		{
			if (sourceType == targetType) {
				return value;
			}

			var destElementType = GetCollectionElementType(targetType);
			var srcElementType = GetCollectionElementType(sourceType);

			var destArgs = destElementType.GetGenericArguments();
			var destKeyType = destArgs[0];
			var destValueType = destArgs[1];

			var srcArgs = srcElementType.GetGenericArguments();
			var srcKeyType = srcArgs[0];
			var srcValueType = srcArgs[1];

			if (destElementType != null) {
				var activator = dictionaryActivator.MakeGenericMethod(srcKeyType, srcValueType, destKeyType, destValueType);
				var target = Activate(targetType);
				return activator.Invoke(this, new object[] { aggRoot, target, value, direction });
			}

			return null;
		}

		private object InternalGenerateCollection<TSrc, TDest>(AggregateRoot root, ICollection<TDest> target, ICollection<TSrc> source, TransferDirection direction)
		{
			var constructor = GetObjectConstructor(typeof(TDest));
			Action<TSrc, TDest> transfer;

			if (direction == TransferDirection.ToAggregateRoot) {
				transfer = (src, dest) => InternalTransfer(root, src, dest, direction);
			} else {
				transfer = (src, dest) => InternalTransfer(root, dest, src, direction);
			}

			foreach (var item in source) {
				var newItem = (TDest)constructor(root, item);
				transfer(item, newItem);
				target.Add(newItem);
			}

			return target;
		}

		private object InternalGenerateDictionary<TSrcKey, TSrc, TDestKey, TDest>(AggregateRoot root, IDictionary<TDestKey, TDest> target, IDictionary<TSrcKey, TSrc> source, TransferDirection direction)
		{
			var destKey = typeof(TDestKey);
			var srcKey = typeof(TSrcKey);

			var valueConstructor = GetObjectConstructor(typeof(TDest));

			Action<TSrc, TDest> valueTransfer;
			Func<TSrcKey, object> keyTransfer = (src) => {
				if (srcKey == destKey) {
					return (TDestKey)(object)src;
				}
				var newKey = (TDestKey)Activate(destKey);
				InternalTransfer(root, src, newKey, direction);
				return newKey;
			};

			if (direction == TransferDirection.ToAggregateRoot) {
				valueTransfer = (src, dest) => InternalTransfer(root, src, dest, direction);
			} else {
				valueTransfer = (src, dest) => InternalTransfer(root, dest, src, direction);
			}

			foreach (var item in source) {
				var newItem = (TDest)valueConstructor(root, item.Value);

				valueTransfer(item.Value, newItem);

				target.Add((TDestKey)keyTransfer(item.Key), newItem);
			}

			return target;
		}

		private void InternalTransfer<T1, T2>(AggregateRoot root, T1 snapshot, T2 target, TransferDirection direction)
		{
			var aggregateFieldMap = SnapshotableField.GetMap(target.GetType());
			var snapshotFields = snapshot.GetType().GetFields(BindingFlags.Instance | BindingFlags.Public);

			Action<object, FieldInfo, object, FieldInfo> doTransfer = null;

			if (direction == TransferDirection.ToAggregateRoot) {
				doTransfer = (source, sourceField, destination, destinationField)
					=> SetupTransfer(root, target, direction, source, sourceField, destination, destinationField);
			} else {
				doTransfer = (destination, destinationField, source, sourceField)
					=> SetupTransfer(root, target, direction, source, sourceField, destination, destinationField);
			}

			foreach (var snapshotField in snapshotFields) {
				FieldInfo targetField;
				if (aggregateFieldMap.TryGetValue(snapshotField.Name, out targetField)) {
					doTransfer(snapshot, snapshotField, target, targetField);
				} else {
					// TODO: No field found; throw?
				}
			}
		}

		private void SetupTransfer<T2>(AggregateRoot root, T2 target, TransferDirection direction, object source, FieldInfo sourceField, object destination, FieldInfo destinationField)
		{
			destinationField.SetValue(destination, Translate(root, destinationField, source, sourceField, direction));
		}

		private void TransferState(DynamicSnapshotBase snapshot, AggregateRoot aggregate, TransferDirection direction)
		{
			Contract.Requires(snapshot != null, "Must provide a snapshot");
			Contract.Requires(aggregate != null, "Must provide an aggregate root");

			InternalTransfer(aggregate, snapshot, aggregate, direction);
		}

		private object Translate<T>(AggregateRoot root, FieldInfo destinationField, T source, FieldInfo sourceField, TransferDirection direction)
		{
			// key.Item1 is aggregate, key.Item2 is snapshot

			var activator = activators.GetOrAdd(new Tuple<Type, Type, TransferDirection>(sourceField.FieldType, destinationField.FieldType, direction), (Tuple<Type, Type, TransferDirection> key) => {
				if (!key.Item1.RequiresCustomSnapshotting() && !key.Item2.RequiresCustomSnapshotting()) {
					return (aggRoot, src) => {
						return src;
					};
				}

				// TODO support Entities
				var resultType = destinationField.FieldType;
				CandidateType candidateType = direction == TransferDirection.ToSnapshot ? key.Item1.RequiresCustomSnapshotting() : key.Item2.RequiresCustomSnapshotting();

				switch (candidateType) {
					case CandidateType.NonSerializable:
						return (aggRoot, src) => {
							var target = GetObjectConstructor(resultType)(aggRoot, src);
							InternalTransfer(aggRoot, src, target, key.Item3);
							return target;
						};

					case CandidateType.Collection:
						if (key.Item1 == key.Item2) {
							return (aggRoot, src) => src;
						}

						var destElementType = GetCollectionElementType(key.Item2);
						var srcElementType = GetCollectionElementType(key.Item1);
						var collectionActivator = SnapshotableImplementer<TSnapshot>.collectionActivator.MakeGenericMethod(srcElementType, destElementType);

						return (aggRoot, src) => {
							if (destElementType != null) {
								var targetCollection = Activate(key.Item2);
								return collectionActivator.Invoke(this, new object[] { aggRoot, targetCollection, src, direction });
							}

							return null;
						};

					case CandidateType.Dictionary:
						return (aggRoot, src) => {
							return GenerateDictionary(aggRoot, resultType, src, key.Item1, key.Item3);
						};
				}

				return (aggRoot, src) => null;
			});

			var value = sourceField.GetValue(source);
			return activator(root, value);
		}
	}
}