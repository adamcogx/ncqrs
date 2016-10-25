using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ncqrs.Eventing
{
	[ContractClass(typeof(IKnownCommandsEnumeratorContracts))]
	public interface IKnownEventsEnumerator
	{
		IEnumerable<Type> GetAllEventTypes();
	}

	[ContractClassFor(typeof(IKnownEventsEnumerator))]
	internal abstract class IKnownCommandsEnumeratorContracts : IKnownEventsEnumerator
	{
		public IEnumerable<Type> GetAllEventTypes()
		{
			Contract.Ensures(Contract.Result<IEnumerable<Type>>() != null);
			return default(IEnumerable<Type>);
		}
	}
}
