using PLang.Building.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PLang.Modules.InjectModule
{
	public class Builder : BaseBuilder
	{
		public Builder() : base() { }

		public override async Task<Instruction> Build(GoalStep goalStep)
		{
			var setup = (goalStep.RelativePrPath.ToLower().StartsWith("setup")) ? "true" : "false";
			AppendToSystemCommand($@"
if user does not define if injection is global for whole app, then globalForWholeApp={setup}
");

			var instruction = await base.Build(goalStep);

			var gf = instruction.Action as GenericFunction;
			
			return instruction;

		}


	}
}
