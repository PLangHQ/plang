using PLang.Building.Events;
using PLang.Building.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PLang.Exceptions
{
	public class RuntimeEventException : Exception
	{
		EventBinding? eventModel;
		public RuntimeEventException(string message, EventBinding? eventModel = null) : base(message) {
			this.eventModel = eventModel;
		}
	}
}
