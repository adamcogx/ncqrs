using Ncqrs.Commanding.CommandExecution.Mapping;
using Ncqrs.Commanding.CommandExecution.Mapping.Attributes;
using System;
using System.Linq;
using System.Collections.Generic;
using System.Reflection;

namespace Ncqrs.Domain.Storage
{
	public class SimpleAggregateRootCreationStrategy
		: AggregateRootCreationStrategy
	{

		private static Dictionary<Tuple<Type, bool>, Func<Guid?, AggregateRoot>> cachedCtorMappings = new Dictionary<Tuple<Type, bool>, Func<Guid?, AggregateRoot>>();

		protected override AggregateRoot CreateAggregateRootFromType(Type aggregateRootType)
		{
			var key = new Tuple<Type, bool>(aggregateRootType, false);
			if (!cachedCtorMappings.ContainsKey(key)) {
				var flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

				AggregateRoot aggregateRoot = null;
				// Get the constructor that we want to invoke.
				var ctor = aggregateRootType.GetConstructor(flags, null, Type.EmptyTypes, null);

				// If there was no ctor found, throw exception.
				if (ctor == null) {
					var message = String.Format("No constructor found on aggregate root type {0} that accepts " +
												"no parameters.", aggregateRootType.AssemblyQualifiedName);
					throw new AggregateRootCreationException(message);
				}

				// There was a ctor found, so invoke it and return the instance.
				cachedCtorMappings[key] = x => (AggregateRoot)ctor.Invoke(null);
			}

			return cachedCtorMappings[key](null);
		}

		protected override AggregateRoot CreateAggregateRootFromType(Type aggregateRootType, Guid? id)
		{
			var key = new Tuple<Type, bool>(aggregateRootType, true);
			if (!cachedCtorMappings.ContainsKey(key)) {

				// Flags to search for a public and non public contructor.
				var flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

				AggregateRoot aggregateRoot = null;

				// Get the constructor that we want to invoke.
				var ctor = aggregateRootType.GetConstructor(flags, null, new Type[] { typeof(Guid) }, null);

				if (ctor == null) {
					var message = String.Format("No constructor found on aggregate root type {0} that accepts " +
												"an Guid id", aggregateRootType.AssemblyQualifiedName);
					throw new AggregateRootCreationException(message);
				}

				cachedCtorMappings[key] = x => (AggregateRoot)ctor.Invoke(new object[] { x.Value });
			}

			return cachedCtorMappings[key](id);
		}

		private static Dictionary<Tuple<Type, Type>, Tuple<ConstructorInfo, PropertyInfo[]>> cachedCommandCtorMappings = new Dictionary<Tuple<Type, Type>, Tuple<ConstructorInfo, PropertyInfo[]>>();

		protected override AggregateRoot CreateAggregateRootFromTypeAndCommand(Type aggregateRootType, Commanding.ICommand command)
		{
			var cached = GetCachedCommandConstructorMapping(aggregateRootType, command);

			var parameter = cached.Item2.Select(p => p.GetValue(command, null));
			return (AggregateRoot)cached.Item1.Invoke(parameter.ToArray());
		}

		protected Tuple<ConstructorInfo, PropertyInfo[]> GetCachedCommandConstructorMapping(Type aggregateRootType, Commanding.ICommand command)
		{
			var key = Tuple.Create(aggregateRootType, command.GetType());

			lock (cachedCommandCtorMappings) {
				if (!cachedCommandCtorMappings.ContainsKey(key)) {
					var match = GetMatchingConstructor(aggregateRootType, command.GetType());
					cachedCommandCtorMappings.Add(key, match);
				}
			}

			var cached = cachedCommandCtorMappings[key];
			return cached;
		}

		protected Tuple<ConstructorInfo, PropertyInfo[]> GetMatchingConstructor(Type aggType, Type commandType)
		{
			var strategy = new AttributePropertyMappingStrategy();
			var sources = strategy.GetMappedProperties(commandType);

			return PropertiesToMethodMapper.GetConstructor(sources, aggType);
		}

		protected PropertyToParameterMappingInfo[] GetMappedProperties(Type target)
		{
			// TODO: At support for both: exclude and include strategy.
			return target.GetProperties().Where
				(
					p => !p.IsDefined(typeof(ExcludeInMappingAttribute), false)
				).Select(FromPropertyInfo).ToArray();
		}

		private PropertyToParameterMappingInfo FromPropertyInfo(PropertyInfo prop)
		{
			int? ordinal = null;
			string name = prop.Name;

			var attr = (ParameterAttribute)prop.GetCustomAttributes(typeof(ParameterAttribute), false).FirstOrDefault();

			if (attr != null) {
				ordinal = attr.Ordinal;
				name = attr.Name ?? name;
			}

			return new PropertyToParameterMappingInfo(ordinal, name, prop);
		}
	}
}
