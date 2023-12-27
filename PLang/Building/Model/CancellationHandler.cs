using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PLang.Building.Model
{
	public class CancellationHandler
	{
		public long CancelExecutionAfterXMilliseconds {get;set;}
		public string GoalNameToCallAfterCancellation { get; set; }
	}
}
