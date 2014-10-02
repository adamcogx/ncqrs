using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace Ncqrs.Commanding.CommandExecution.Mapping.Attributes
{
    public class AttributePropertyMappingStrategy
    {
        private static readonly Dictionary<Type, PropertyToParameterMappingInfo[]> mappingCache = new Dictionary<Type, PropertyToParameterMappingInfo[]>();

        public PropertyToParameterMappingInfo[] GetMappedProperties(Type target)
        {
            lock (mappingCache)
            {
                if (!mappingCache.ContainsKey(target))
                {
                    // TODO: At support for both: exclude and include strategy.
                    var mapping = target.GetProperties()
                        .Where(p => !p.IsDefined(typeof(ExcludeInMappingAttribute), false))
                        .Select(FromPropertyInfo).ToArray();

                    mappingCache.Add(target, mapping);

                    return mapping;
                }
                else
                {
                    return mappingCache[target];
                }
            }
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
