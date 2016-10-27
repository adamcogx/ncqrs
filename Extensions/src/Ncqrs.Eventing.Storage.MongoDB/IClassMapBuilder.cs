using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using MongoDB.Bson.Serialization;

namespace Ncqrs.Eventing.Storage.MongoDB
{
    public interface IClassMapBuilder
    {
        IEnumerable<BsonClassMap> Build();
    }
}
