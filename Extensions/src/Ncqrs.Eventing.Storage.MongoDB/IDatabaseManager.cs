using System.Collections.Generic;
using MongoDB.Driver;

namespace Ncqrs.Eventing.Storage.MongoDB
{
	public interface IDatabaseManager
	{
		IEnumerable<string> CollectionNames
		{
			get;
		}

		IMongoCollection<T> GetCollection<T>(string collectionName);
	}
}