using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using MongoDB.Bson;

namespace Ncqrs.EventBus
{
	// [PipelineName], [LastProcessedEventId]
	class PipelineState
	{
		public long Id
		{
			get; set;
		}

		public string PipelineName
		{
			get; set;
		}

		public Guid LastProcessedEventId
		{
			get; set;
		}
	}
}
