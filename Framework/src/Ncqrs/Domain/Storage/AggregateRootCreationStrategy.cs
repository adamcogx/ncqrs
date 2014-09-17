using Ncqrs.Commanding.CommandExecution.Mapping;
using System;
using System.Reflection;
using System.Linq;
using Ncqrs.Commanding.CommandExecution.Mapping.Attributes;
using System.Collections.Generic;

namespace Ncqrs.Domain.Storage
{
    public abstract class AggregateRootCreationStrategy 
        : IAggregateRootCreationStrategy
    {
        public AggregateRoot CreateAggregateRoot(Type aggregateRootType)
        {
            if (!aggregateRootType.IsSubclassOf(typeof(AggregateRoot)))
            {
                var msg = string.Format("Specified type {0} is not a subclass of AggregateRoot class.", aggregateRootType.FullName);
                throw new ArgumentOutOfRangeException("aggregateRootType", msg);
            }

            return CreateAggregateRootFromType(aggregateRootType);
        }

        protected abstract AggregateRoot CreateAggregateRootFromType(Type aggregateRootType);

        public T CreateAggregateRoot<T>() where T : AggregateRoot
        {
            return (T)CreateAggregateRoot(typeof(T));
        }

        private static Dictionary<Tuple<Type, Type>, Tuple<ConstructorInfo, PropertyInfo[]>> cachedConstructors = new Dictionary<Tuple<Type, Type>, Tuple<ConstructorInfo, PropertyInfo[]>>();

        public AggregateRoot CreateAggregateRootFromCommand(Type aggregateRootType, Commanding.ICommand command)
        {
            var key = Tuple.Create(aggregateRootType, command.GetType());

            if (!cachedConstructors.ContainsKey(key))
            {
                var match = GetMatchingConstructor(aggregateRootType, command.GetType());
                cachedConstructors.Add(key, match);
            }

            var cached = cachedConstructors[key];
            var parameter = cached.Item2.Select(p => p.GetValue(command, null));
            return (AggregateRoot)cached.Item1.Invoke(parameter.ToArray());
        }

        public T CreateAggregateRootFromCommand<T>(Commanding.ICommand command) where T : AggregateRoot
        {
            return (T)CreateAggregateRootFromCommand(typeof(T), command);
        }

        private static Tuple<ConstructorInfo, PropertyInfo[]> GetMatchingConstructor(Type aggType, Type commandType)
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

            if (attr != null)
            {
                ordinal = attr.Ordinal;
                name = attr.Name ?? name;
            }

            return new PropertyToParameterMappingInfo(ordinal, name, prop);
        }
    }
}
