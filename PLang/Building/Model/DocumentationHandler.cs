using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PLang.Building.Model
{
	public class ExternalServiceHandler
	{
		public string GoalName { get; set; }
		public string Uri { get; set; }
		public string StartCssSelector { get; set; }
		public string EndCssSelector { get; set;}
	}
}
