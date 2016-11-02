using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using MongoDB.Bson.Serialization;
using MongoDB.Driver;

namespace Ncqrs.Eventing.Storage.MongoDB
{
	public class DatabaseManager : IDatabaseManager
	{
		private readonly IMongoDatabase database;

		public DatabaseManager(string serverUrl, string dbName, Func<IEnumerable<BsonClassMap>> classMaps)
		{
			var client = new MongoClient(serverUrl);
			var databases = client.ListDatabases().ToList().Select(x => x.GetValue("name").AsString).ToList();
			var existingName = databases.Where(x => x.Equals(dbName, StringComparison.OrdinalIgnoreCase)).FirstOrDefault();
			database = client.GetDatabase(existingName ?? dbName);
			foreach (var map in classMaps()) {
				BsonClassMap.RegisterClassMap(map);
			}
		}

		public IMongoCollection<T> GetCollection<T>(string collectionName)
		{
			return database.GetCollection<T>(collectionName);
		}

		public IEnumerable<String> CollectionNames
		{
			get {
				return database.ListCollections().ToList().Select(x => x.GetValue("name").AsString).ToList();
			}
		}
	}
}
