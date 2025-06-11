using PLang.Attributes;
using PLang.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PLang.Building.Model
{
	public class CancellationHandler
	{
		[DefaultValueAttribute(30)]
		public long? CancelExecutionAfterXMilliseconds {get;set;} = 30;
		[DefaultValueAttribute(null)]
		public GoalToCallInfo? GoalNameToCallAfterCancellation { get; set; } = null;
	}
}
