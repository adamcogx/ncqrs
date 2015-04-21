using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;

namespace Ncqrs.Eventing.Sourcing.Mapping
{
    /// <summary>
    /// A internal event handler mapping strategy that maps methods as an event handler based on method name and parameter type.
    /// <remarks>
    /// All method that match the following requirements are mapped as an event handler:
    /// <list type="number">
    ///     <item>
    ///         <value>
    ///             Method name should start with <i>On</i> or <i>on</i>. Like: <i>OnProductAdded</i> or <i>onProductAdded</i>.
    ///         </value>
    ///     </item>
    ///     <item>
    ///         <value>
    ///             The method should only accept one parameter.
    ///         </value>
    ///     </item>
    ///     <item>
    ///         <value>
    ///             The parameter must be, or implemented from, the type specified by the <see cref="EventBaseType"/> property. Which is <see cref="Object"/> by default.
    ///         </value>
    ///     </item>
    /// </list>
    /// </remarks>
    /// </summary>
    public class ConventionBasedEventHandlerMappingStrategy : IEventHandlerMappingStrategy
    {
        private static readonly ILog Logger = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        private Type _eventBaseType;

        public Type EventBaseType
        {
            get { return _eventBaseType; }
            set
            {
                lock (typeMatchedMethods)
                {
                    _eventBaseType = value;
                    typeMatchedMethods.Clear();
                }
            }
        }

        public String MethodNameRegexPattern { get; set; }

        public ConventionBasedEventHandlerMappingStrategy()
        {
            MethodNameRegexPattern = "^(on|On|ON)+";
            EventBaseType = typeof(Object);
        }

        private static Dictionary<Tuple<MethodBase, Type>, MethodInfo> cachedEventHandlerMethods = new Dictionary<Tuple<MethodBase, Type>, MethodInfo>();
        private Dictionary<Type, IEnumerable<MatchedMethods>> typeMatchedMethods = new Dictionary<Type, IEnumerable<MatchedMethods>>();

        public IEnumerable<ISourcedEventHandler> GetEventHandlers(object target)
        {
            Contract.Requires<ArgumentNullException>(target != null, "The target cannot be null.");
            Contract.Ensures(Contract.Result<IEnumerable<ISourcedEventHandler>>() != null, "The result should never be null.");

            var targetType = target.GetType();
            var handlers = new List<ISourcedEventHandler>();

            lock (typeMatchedMethods)
            {
                if (!typeMatchedMethods.ContainsKey(targetType))
                {

                    Logger.DebugFormat("Trying to get all event handlers based by convention for {0}.", targetType);

                    var methodsToMatch = targetType.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

                    var matchedMethods = (from method in methodsToMatch
                                          let parameters = method.GetParameters()
                                          let noEventHandlerAttributes =
                                              method.GetCustomAttributes(typeof(NoEventHandlerAttribute), true)
                                          where
                                              // Get only methods where the name matches.
                                             Regex.IsMatch(method.Name, MethodNameRegexPattern, RegexOptions.CultureInvariant) &&
                                              // Get only methods that have 1 parameter.
                                             parameters.Length == 1 &&
                                              // Get only methods where the first parameter is an event.
                                             EventBaseType.IsAssignableFrom(parameters[0].ParameterType) &&
                                              // Get only methods that are not marked with the no event handler attribute.
                                             noEventHandlerAttributes.Length == 0
                                          select
                                             new MatchedMethods { MethodInfo = method, FirstParameter = method.GetParameters()[0] }).ToList();

                    typeMatchedMethods.Add(targetType, matchedMethods);
                }
            }

            foreach (var method in typeMatchedMethods[targetType])
            {
                var methodCopy = method.MethodInfo;

                Action<object> invokeAction;

                if (methodCopy.IsGenericMethodDefinition)
                {

                    invokeAction = (e) =>
                    {
                        var key = Tuple.Create((MethodBase)methodCopy, e.GetType());
                        var tempCopy = methodCopy;
                        lock (cachedEventHandlerMethods)
                        {
                            if (cachedEventHandlerMethods.ContainsKey(key))
                            {
                                tempCopy = cachedEventHandlerMethods[key];
                            }
                            else
                            {
                                var arguments = tempCopy.GetGenericArguments();
                                var eventType = e.GetType();
                                if (arguments.Length == 1)
                                {
                                    var eventArgs = eventType.GetGenericArguments();
                                    foreach (var eventArg in eventArgs)
                                    {
                                        if (arguments[0].BaseType.IsAssignableFrom(eventArg))
                                        {
                                            tempCopy = tempCopy.MakeGenericMethod(eventArg);
                                            break;
                                        }
                                    }
                                }
                                cachedEventHandlerMethods[key] = tempCopy;
                            }
                        }

                        tempCopy.Invoke(target, new[] { e });
                    };
                }
                else
                {
                    invokeAction = (e) => methodCopy.Invoke(target, new[] { e });
                }

                Logger.DebugFormat("Created event handler for method {0} based on convention.", methodCopy.Name);

                var handler = new TypeThresholdedActionBasedDomainEventHandler(invokeAction, method.FirstParameter.ParameterType, methodCopy.Name, true);
                handlers.Add(handler);
            }

            return handlers;
        }
    }
}