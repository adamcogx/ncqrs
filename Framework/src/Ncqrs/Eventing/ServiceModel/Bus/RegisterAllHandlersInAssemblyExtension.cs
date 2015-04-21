using System;
using System.Diagnostics.Contracts;
using System.Linq;
using System.Reflection;

namespace Ncqrs.Eventing.ServiceModel.Bus
{
    public static class RegisterAllHandlersInAssemblyExtension
    {
        private static ILog _log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        /// <summary>
        /// Registers all types that implement <see cref="Ncqrs.Eventing.ServiceModel.Bus.IEventHandler"/> as handlers for the given event.
        /// </summary>
        /// <param name="target">The <see cref="InProcessEventBus"/> to register the handlers in.</param>
        /// <param name="asm">The assembly containing the types to register</param>
        public static void RegisterAllHandlersInAssembly(this InProcessEventBus target, Assembly asm)
        {
            target.RegisterAllHandlersInAssembly(asm, CreateInstance);
        }

        /// <summary>
        /// Registers all types that implement <see cref="Ncqrs.Eventing.ServiceModel.Bus.IEventHandler"/> as handlers for the given event using
        /// the given function to instantiate the type.
        /// </summary>
        /// <param name="target">The <see cref="InProcessEventBus"/> to register the handlers in.</param>
        /// <param name="asm">The assembly containing the types to register</param>
        /// <param name="handlerFactory">The function used to instantiate the given handler type</param>
        public static void RegisterAllHandlersInAssembly(this InProcessEventBus target, Assembly asm, Func<Type, object> handlerFactory)
        {
            foreach (var type in asm.GetTypes().Where(x => ImplementsAtLeastOneIEventHandlerInterface(x) && IsConcrete(x)))
            {
                var handler = handlerFactory(type);

                foreach(var handlerInterfaceType in type.GetInterfaces().Where(IsIEventHandlerInterface))
                {
                    var eventDataType = handlerInterfaceType.GetGenericArguments().First();
                    RegisterHandler(handler, eventDataType, target);
                }
            }
        }

        /// <summary>
        /// Registers all types that implement <see cref="Ncqrs.Eventing.ServiceModel.Bus.IEventHandler"/> and match the given criteria as handlers for the given event.
        /// </summary>
        /// <param name="target">The <see cref="InProcessEventBus"/> to register the handlers in.</param>
        /// <param name="asm">The assembly containing the types to register</param>
        /// <param name="matching">Function that decides if the type should be registered as a handler</param>
        public static void RegisterAllHandlersInAssemblyMatching(this InProcessEventBus target, Assembly asm, Func<Type, bool> matching)
        {
            target.RegisterAllHandlersInAssemblyMatching(asm, matching, CreateInstance);
        }

        /// <summary>
        /// Registers all types that implement <see cref="Ncqrs.Eventing.ServiceModel.Bus.IEventHandler"/> and match the given criteria as handlers for the given event
        /// using the given factory method to instantiate the handler.
        /// </summary>
        /// <param name="target">The <see cref="InProcessEventBus"/> to register the handlers in.</param>
        /// <param name="asm">The assembly containing the types to register</param>
        /// <param name="matching">Function that decides if the type should be registered as a handler</param>
        /// <param name="handlerFactory">The function used to instantiate the given handler type</param>
        public static void RegisterAllHandlersInAssemblyMatching(this InProcessEventBus target, Assembly asm, Func<Type, bool> matching, Func<Type, object> handlerFactory)
        {
            foreach (var type in asm.GetTypes().Where(x => matching(x) && ImplementsAtLeastOneIEventHandlerInterface(x) && IsConcrete(x)))
            {
                var handler = handlerFactory(type);

                foreach (var handlerInterfaceType in type.GetInterfaces().Where(IsIEventHandlerInterface))
                {
                    var eventDataType = handlerInterfaceType.GetGenericArguments().First();
                    RegisterHandler(handler, eventDataType, target);
                }
            }
        }

        private static bool IsConcrete(Type x)
        {
            return !x.ContainsGenericParameters;
        }

        private static object CreateInstance(Type type)
        {
            Contract.Requires<ArgumentNullException>(type != null);
            return Activator.CreateInstance(type);
        }

        private static void RegisterHandler(object handler, Type eventDataType, InProcessEventBus target)
        {
            var registerHandlerMethod = target.GetType().GetMethods().Single
            (
                m => m.Name == "RegisterHandler" && m.IsGenericMethod && m.GetParameters().Count() == 1
            );

            var targetMethod = registerHandlerMethod.MakeGenericMethod(new[] { eventDataType });
            targetMethod.Invoke(target, new object[] { handler });

            _log.InfoFormat("Registered {0} as event handler for event {1}.", handler.GetType().FullName, eventDataType.FullName);
        }

        private static bool ImplementsAtLeastOneIEventHandlerInterface(Type type)
        {
            return type.IsClass && !type.IsAbstract &&
                   type.GetInterfaces().Any(IsIEventHandlerInterface);
        }

        private static bool IsIEventHandlerInterface(Type type)
        {
            if(type.IsInterface &&
                   type.IsGenericType &&
                   type.GetGenericTypeDefinition() == typeof(IEventHandler<>))
            {
                var parm = type.GetGenericArguments()[0];
                return !parm.IsGenericType;
            }

            return false;
        }
    }
}
