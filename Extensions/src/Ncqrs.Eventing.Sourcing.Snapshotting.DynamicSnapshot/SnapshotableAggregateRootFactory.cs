using System;
using Castle.DynamicProxy;
using Ncqrs.Domain;
using System.Linq;
using System.Collections.Generic;
using System.Reflection;

namespace Ncqrs.Eventing.Sourcing.Snapshotting.DynamicSnapshot
{
    public class SnapshotableAggregateRootFactory
    {
        private readonly SnapshotableImplementerFactory _snapshotableImplementerFactory;
        private readonly IDynamicSnapshotAssembly _dynamicSnapshotAssembly;
        private readonly Dictionary<Type, ConstructorInfo> proxyCache = new Dictionary<Type, ConstructorInfo>();

        public SnapshotableAggregateRootFactory(IDynamicSnapshotAssembly dynamicSnapshotAssembly, SnapshotableImplementerFactory snapshotableImplementerFactory)
        {
            _snapshotableImplementerFactory = snapshotableImplementerFactory;
            _dynamicSnapshotAssembly = dynamicSnapshotAssembly;
        }

        public AggregateRoot Create(Type aggregateType)
        {
            if (!typeof(AggregateRoot).IsAssignableFrom(aggregateType))
                throw new ArgumentException("aggregateType must inherit AggregateRoot");

            var snapshotType = _dynamicSnapshotAssembly.FindSnapshotType(aggregateType);
            var snapshotableImplementer = _snapshotableImplementerFactory.Create(snapshotType);

            if (!proxyCache.ContainsKey(aggregateType))
            {
                var generator = new ProxyGenerator();

                var options = new ProxyGenerationOptions();
                options.AddMixinInstance(snapshotableImplementer);

                var proxyTemp = (AggregateRoot)generator.CreateClassProxy(aggregateType, options);

                var constructors = proxyTemp.GetType().GetConstructors();

                foreach (var constructor in constructors)
                {
                    if (constructor.GetParameters().Count() == 4)
                    {
                        proxyCache[aggregateType] = constructor;
                        break;
                    }
                }
            }

            var proxy = (AggregateRoot)proxyCache[aggregateType].Invoke(new object[] { snapshotableImplementer, snapshotableImplementer, snapshotableImplementer, null });
            ((IHaveProxyReference)proxy).Proxy = proxy;

            return proxy;
        }

    }
}
