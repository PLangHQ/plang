using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PLang.Exceptions
{
	public class AIServiceException : Exception
	{
		string system;
		string assistant;
		string user;

		public AIServiceException(string message, string system, string assistant, string user) : base(message) {
			this.system = system;
			this.assistant = assistant;
			this.user = user;
		}
	}
}
