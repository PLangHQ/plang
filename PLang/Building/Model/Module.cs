using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PLang.Building.Model
{
	public record Module(string Name, string StepNr, bool RunOnce, object Object, DateTime? Executed = null);
}
