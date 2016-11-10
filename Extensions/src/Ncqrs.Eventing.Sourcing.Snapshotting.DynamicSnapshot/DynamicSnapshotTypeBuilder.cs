using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using Ncqrs.Domain;

namespace Ncqrs.Eventing.Sourcing.Snapshotting.DynamicSnapshot
{
    public class DynamicSnapshotTypeBuilder
    {
        private readonly Type SnapshotBaseType = typeof(DynamicSnapshotBase);

        /// <summary>
        /// Creates a snapshot type from aggregate.
        /// </summary>
        /// <param name="aggregateType">Type of the aggregate.</param>
        /// <param name="moduleBuilder">The module builder.</param>
        /// <returns></returns>
        public Type CreateType(Type aggregateType, ModuleBuilder moduleBuilder, Dictionary<Type, Type> typeRegistry)
		{
			if (aggregateType == null)
				throw new ArgumentNullException("sourceType");
			if (moduleBuilder == null)
				throw new ArgumentNullException("moduleBuilder");

			Guard(aggregateType);

			Type snapshot = InternalCreateType(aggregateType, moduleBuilder, typeRegistry);

			return snapshot;
		}

		private Type InternalCreateType(Type sourceType, ModuleBuilder moduleBuilder, Dictionary<Type, Type> typeRegistry)
		{
			if (typeRegistry.ContainsKey(sourceType)) {
				return typeRegistry[sourceType];
			}

			var typeBuilder = GetTypeBuilder(sourceType, moduleBuilder);
			CreateConstructor(typeBuilder);
			CreateFields(sourceType, typeBuilder, moduleBuilder, typeRegistry ?? new Dictionary<Type, Type>());

			var newType = typeBuilder.CreateType();
			typeRegistry[sourceType] = newType;

			return newType;
		}

		private void CreateConstructor(TypeBuilder typeBuilder)
        {
            var ctor = SnapshotBaseType.GetConstructors(BindingFlags.Public | BindingFlags.Instance).First();
            var ctorSignature = ctor.GetParameters().Select(p => p.ParameterType);

            var ctorBuilder = typeBuilder.DefineConstructor(MethodAttributes.Public, CallingConventions.Standard, ctorSignature.ToArray());
            var il = ctorBuilder.GetILGenerator();

            il.Emit(OpCodes.Ldarg_0); // push this

            int paramIndex = 1;
            foreach (var param in ctorSignature) // push all constructor parameters onto the stack
            {
                il.Emit(OpCodes.Ldarg, paramIndex++);
            }

            il.Emit(OpCodes.Call, ctor); // call DynamicSnapshotBase constructor
            il.Emit(OpCodes.Ret); // return
        }

        private void CreateFields(Type sourceType, TypeBuilder typeBuilder, ModuleBuilder moduleBuilder, Dictionary<Type, Type> typeRegistry)
        {
            var fieldMap = SnapshotableField.GetMap(sourceType);

            foreach (var pair in fieldMap) {
				Type fieldType = pair.Value.FieldType;
				var candidate = fieldType.RequiresCustomSnapshotting();

				if (candidate) {
					fieldType = BuildFieldType(fieldType, candidate, moduleBuilder, typeRegistry);
				}

				typeBuilder.DefineField(pair.Key, fieldType, FieldAttributes.Public);
			}
		}


		private Type BuildFieldType(Type fieldType, CandidateType candidateType, ModuleBuilder moduleBuilder, Dictionary<Type, Type> typeRegistry)
		{
			if (fieldType.IsGenericType) {
				var genType = fieldType.GetGenericTypeDefinition();
				var arguments = fieldType.GetGenericArguments();

				// TODO:  a potential optimization is to only create a new type when either the generic argument (or a
				// contained property) is an Entity.  It will make creation of the types longer but the actual snapshot
				// creation will be faster.

				List<Type> newArguments = new List<Type>();
				foreach (var argument in arguments) {
					newArguments.Add(argument.RequiresCustomSnapshotting() ? InternalCreateType(argument, moduleBuilder, typeRegistry) : argument);
				}

				return genType.MakeGenericType(newArguments.ToArray());
			}

			return InternalCreateType(fieldType, moduleBuilder, typeRegistry);
		}

		private TypeBuilder GetTypeBuilder(Type sourceType, ModuleBuilder moduleBuilder)
        {
            return moduleBuilder.DefineType(
                    SnapshotNameGenerator.Generate(sourceType),
                    TypeAttributes.Class | TypeAttributes.Public | TypeAttributes.Serializable | TypeAttributes.Sealed,
                    SnapshotBaseType);
        }

        private void Guard(Type sourceType)
        {
            bool isSnapshotable = typeof(AggregateRoot).IsAssignableFrom(sourceType) && sourceType.HasAttribute<DynamicSnapshotAttribute>();

            if (!isSnapshotable)
                throw new DynamicSnapshotNotSupportedException() { AggregateType = sourceType };
        }
    }
}