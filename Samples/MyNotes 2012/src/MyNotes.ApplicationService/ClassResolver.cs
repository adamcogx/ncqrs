using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using MongoDB.Bson.Serialization;

namespace MyNotes.ApplicationService
{
	class ClassResolver : Ncqrs.Eventing.Storage.MongoDB.IClassMapBuilder
	{
		private Assembly domainAssembly;

		public ClassResolver(Assembly domainAssembly)
		{
			this.domainAssembly = domainAssembly;
		}

		public IEnumerable<BsonClassMap> Build()
		{
			var types = domainAssembly.DefinedTypes.Select(x => x.AsType());
			foreach (var type in types) {
				var cm = new BsonClassMap(type);
				cm.AutoMap();
				yield return cm;
			}
		}
	}
}
