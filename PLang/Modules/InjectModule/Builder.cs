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
			
			var instruction = await base.Build(goalStep);
			
			return instruction;

		}


	}
}
