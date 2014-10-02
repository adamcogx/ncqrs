using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;

namespace Ncqrs.Eventing.ServiceModel.Bus
{
    public static class OpenGenericsBasedEventHandlersExtensions
    {
        private static readonly MethodInfo dynamicHandlerMethod = typeof(OpenGenericsBasedEventHandlersExtensions)
                .GetMethod("DynamicHandle", BindingFlags.NonPublic | BindingFlags.Static);

        /// <summary>
        /// Allows you to specify an open generic event type and the handler that will handle it.  For Example, you have
        /// a class StateAdded&lt;T&gt; where T : State and StateAddedEventHandler&lt;T&gt; where T : State.  You would say
        /// bus.RegisterOpenGenericEventsHandler(typeof(StateAdded&lt;&gt;), typeof(StateAddedEventHandler&lt;&gt;));
        /// </summary>
        /// <param name="bus"><see cref="InProcessEventBus"/> to register the event handler with</param>
        /// <param name="eventType">the open generic type for the event.</param>
        /// <param name="handlerType">the open generic type for the handler.</param>
        public static void RegisterOpenGenericEventsHandler(this InProcessEventBus bus, Type eventType, Type handlerType)
        {
            bus.RegisterOpenGenericEventsHandler(eventType, handlerType, CreateHandler);
        }

        /// <summary>
        /// Allows you to specify an open generic event type and the handler that will handle it.  For Example, you have
        /// a class StateAdded&lt;T&gt; where T : State and StateAddedEventHandler&lt;T&gt; where T : State.  You would say
        /// bus.RegisterOpenGenericEventsHandler(typeof(StateAdded&lt;&gt;), typeof(StateAddedEventHandler&lt;&gt;), CreateHandler);
        /// </summary>
        /// <param name="bus"><see cref="InProcessEventBus"/> to register the event handler with</param>
        /// <param name="eventType">the open generic type for the event.</param>
        /// <param name="handlerType">the open generic type for the handler.</param>
        /// <param name="handlerFactory">the factory that will instantiate the handler (mainly used for IoC)</param>
        public static void RegisterOpenGenericEventsHandler(this InProcessEventBus bus, Type eventType, Type handlerType, Func<Type, Object> handlerFactory)
        {
            bus.RegisterHandler(eventType, evnt => Handle(handlerType, evnt, handlerFactory));
        }

        private static Dictionary<Type, Action<PublishedEvent>> handlerCache = new Dictionary<Type, Action<PublishedEvent>>();

        private static void Handle(Type handlerRootType, PublishedEvent evnt, Func<Type, Object> handlerFactory)
        {
            var payloadType = evnt.Payload.GetType();

            Action<PublishedEvent> executor;

            lock (handlerCache)
            {
                if (!handlerCache.ContainsKey(payloadType))
                {
                    var arguments = payloadType.GetGenericArguments();
                    var handler = handlerFactory(handlerRootType.MakeGenericType(arguments[0]));
                    var invoker = dynamicHandlerMethod.MakeGenericMethod(payloadType);
                    executor = e => invoker.Invoke(null, new object[] { handler, e });
                    handlerCache.Add(payloadType, executor);
                }
                else
                {
                    executor = handlerCache[payloadType];
                }
            }

            executor(evnt);
        }

        private static void DynamicHandle<T>(IEventHandler<T> handler, IPublishedEvent<T> evnt)
        {
            handler.Handle(evnt);
        }

        private static object CreateHandler(Type handler)
        {
            return Activator.CreateInstance(handler);
        }
    }
}