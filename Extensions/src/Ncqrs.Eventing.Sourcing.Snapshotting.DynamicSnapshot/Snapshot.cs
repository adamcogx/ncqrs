using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Reflection;
using Ncqrs.Domain;

namespace Ncqrs.Eventing.Sourcing.Snapshotting.DynamicSnapshot
{
    public enum CandidateType
    {
        Not, Collection, Dictionary, NonSerializable, Entity,
		NoParameterlessConstructor
	}

    public class CandidateAnalysis
    {
        public CandidateAnalysis(CandidateType type)
        {
            this.Type = type;
        }

        public CandidateType Type
        {
            get;
            private set;
        }

        public static implicit operator bool(CandidateAnalysis analysis)
        {
            return analysis.Type != CandidateType.Not;
        }

        public static implicit operator CandidateType(CandidateAnalysis analysis)
        {
            return analysis.Type;
        }

        public static implicit operator CandidateAnalysis(CandidateType type)
        {
            return new CandidateAnalysis(type);
        }
    }

    public static class SnapshotExtensions
    {
        public static CandidateAnalysis RequiresCustomSnapshotting(this Type fieldType)
        {
            if (!fieldType.IsValueType && fieldType != typeof(string)) {
				if (fieldType.IsGenericType) {
					var genArguments = fieldType.GetGenericArguments();
					if (fieldType.GetInterfaces().Any(x => x.IsOfType(typeof(ICollection<>)) || x.IsOfType(typeof(IDictionary<,>)))) {
						foreach (var parm in genArguments) {
							if (RequiresCustomSnapshotting(parm)) {
								return CandidateType.Collection;
							}
						}
					}
				}

				if (fieldType.IsOfType(typeof(Entity<>))) {
					return CandidateType.Entity;
				}

				var temp = fieldType;
				while (temp != null) {
					if (temp.GetCustomAttribute<SerializableAttribute>(false) == null) {
						return CandidateType.NonSerializable;
					}
					temp = temp.BaseType;
				}
            }

            return CandidateType.Not;
        }

		public static bool HasParameterlessConstructor(this Type type)
		{
			return type
				.GetConstructors(BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance)
				.Any(x => x.GetParameters().Length == 0);
		}
    }
}
