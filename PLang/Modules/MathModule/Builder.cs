using PLang.Building.Model;
using PLang.Errors.Builder;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PLang.Modules.MathModule
{
	public class Builder : BaseBuilder
	{
		public Builder() : base()
		{

		}

		public override async Task<(Instruction?, IBuilderError?)> Build(GoalStep goalStep, IBuilderError? previousBuildError = null)
		{
			AppendToSystemCommand(
				@$"Given a math instruction that is not in a string format, turn it into a string formatted math instruction from the NCalc C# library and passed into the solveExpression function.
				If a trig function wants to be solved by the user and they ask to find the answer in degrees, convert the output to degrees from radians. If they want to find
				the value of the trig function with a parameter in degrees, convert it to radians using 3.14 before passing it in. Also convert pi into 3.14 multiplied by whatever number is next to it if applicable.
				Examples:
				find 3 plus 4 should turn into 3 + 4. Do this with any operator like *, -, +, /
				solve for square root of 9 should turn into Sqrt(9)
				what is 2 raised to the power of 4 should turn into Pow(2, 4)"
				);
			
			return await base.Build(goalStep, previousBuildError);
		}
	}
}
