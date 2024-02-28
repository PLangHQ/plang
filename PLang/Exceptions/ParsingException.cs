using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PLang.Exceptions
{
	public class ParsingException : Exception
	{
		public ParsingException(string message, Exception ex) : base(message, ex) { }
	}
}
