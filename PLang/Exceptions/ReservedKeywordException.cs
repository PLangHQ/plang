using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PLang.Exceptions
{
	public class ReservedKeywordException : Exception
	{
		public ReservedKeywordException(string message) : base(message) { }
	}
}
