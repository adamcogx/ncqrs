using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Ncqrs.Domain;

namespace Ncqrs.Eventing.Sourcing.Snapshotting.DynamicSnapshot
{
	internal static class SnapshotableField
	{
		private static Dictionary<Type, Func<Type, IEnumerable<FieldInfo>>> mappers = new Dictionary<Type, Func<Type, IEnumerable<FieldInfo>>>();

		static SnapshotableField()
		{
			mappers.Add(typeof(AggregateRoot), GetAllForAggegrateRoot);
			mappers.Add(typeof(Entity<>), GetAllForEntity);
		}

		private static IEnumerable<FieldInfo> GetAllForEntity(Type type)
		{
			while (type != null) {
				foreach (var field in GetSnapshotableFields(type))
					if (!typeof(AggregateRoot).IsAssignableFrom(field.FieldType)) {
						yield return field;
					}

				type = type.BaseType;
			}
		}

		public static void AddMapper(Type rootType, Func<Type, IEnumerable<FieldInfo>> mapper)
		{
			mappers.Add(rootType, mapper);
		}

		public static IEnumerable<FieldInfo> GetAll(Type type)
		{
			bool found = false;
			foreach (var mapper in mappers) {
				if (mapper.Key.IsAssignableFrom(type)) {
					found = true;
					foreach (var field in mapper.Value(type))
						yield return field;

					yield break;
				}
			}

			if (!found) {
				foreach (var field in GetAll(type)) {
					yield return field;
				}
			}
		}

		public static IDictionary<string, FieldInfo> GetMap(Type type)
		{
			var map = new Dictionary<string, FieldInfo>();
			var fields = GetAll(type);

			foreach (var field in fields) {
				string key = GenerateFieldKey(field);
				map.Add(key, field);
			}

			return map;
		}

		private static string GenerateFieldKey(FieldInfo field)
		{
			string key = string.Format("{0}_{1}", field.DeclaringType.FullName, field.Name);
			return key;
		}

		private static IEnumerable<FieldInfo> GetAllDefault(Type type)
		{
			while (type != null) {
				foreach (var field in GetSnapshotableFields(type))
					yield return field;

				type = type.BaseType;
			}
		}

		private static IEnumerable<FieldInfo> GetAllForAggegrateRoot(Type type)
		{
			while (type != null) {
				if (type.HasAttribute<DynamicSnapshotAttribute>())
					foreach (var field in GetSnapshotableFields(type))
						yield return field;

				type = type.BaseType;
			}
		}

		private static IEnumerable<FieldInfo> GetSnapshotableFields(Type sourceType)
		{
			BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;
			return sourceType.GetFields(flags).Where(field => IsSnapshotable(field));
		}

		private static bool IsSnapshotable(FieldInfo field)
		{
			return field.GetCustomAttributes(typeof(ExcludeFromSnapshotAttribute), false).Count() == 0;
		}
	}
}