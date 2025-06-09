using PLang.Building.Model;
using PLang.Errors.Builder;
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

		public override async Task<(Instruction?, IBuilderError?)> Build(GoalStep goalStep, IBuilderError? previousBuildError = null)
		{
			var setup = (goalStep.RelativePrPath.ToLower().StartsWith("setup")) ? "true" : "false";
			AppendToSystemCommand($@"
if user does not define if injection is global for whole app, then globalForWholeApp={setup}
");

			return await base.Build(goalStep, previousBuildError);
		}


	}
}
