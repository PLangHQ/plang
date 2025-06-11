using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using PLang.Building.Model;
using PLang.Building.Parsers;
using PLang.Errors;
using PLang.Errors.Builder;
using PLang.Models;
using PLang.Runtime;
using PLang.Utils;

namespace PLang.Modules.CallGoalModule
{
	public class Builder : BaseBuilder
	{
		private readonly IGoalParser goalParser;
		private readonly PrParser prParser;
		private readonly MemoryStack memoryStack;
		private readonly ILogger logger;

		public Builder(IGoalParser goalParser, PrParser prParser, MemoryStack memoryStack, ILogger logger)
		{
			this.goalParser = goalParser;
			this.prParser = prParser;
			this.memoryStack = memoryStack;
			this.logger = logger;
		}

		public override async Task<(Instruction?, IBuilderError?)> Build(GoalStep step, IBuilderError? previousBuildError = null)
		{
			AppendToSystemCommand(@"
Only choose RunApp if user specifically defines it as app

Use <examples> to understand user input

<examples>
call goal /Start => function name:RunGoal  goalName:/Start, that is rooted, no parameter.
call ParseText => function name:RunGoal, goalName: ParseText, no parameters
run Folder/Search q=%fileName% => function: RunGoal, goalName:Folder/Search,, parameter key is q, and value is %fileName%
execute goal EvaluteScore %user%, write to %score% => function: RunGoal, goalName:EvaluteScore, parameter is user=%user%, return value is %score%
set %data% = ParseDocument(%document%) => function: RunGoal, goalName:ParseDocument, parameter is %document%, %data% is the return value
%user% = GetUser %id% => function: RunGoal, goalName:GetUser, parameter is %id%, return value is %user%

call app Gmail/Search %query% => function name:RunApp, appName:Gmail, goalName:/Search , %query% is key and value in parameters
call app /Builder/DbModule %content%, write to %result% => function name:RunApp, appName:Builder, goalName:/DbModule , %query% is key and value in parameters, return value %result%
<examples>
""");
			
			var build = await base.Build(step, previousBuildError);
			return build;
		}

		private static List<Goal>? allGoals = null;

		public async Task<(Instruction?, IBuilderError?)> BuilderRunApp(GoalStep step, Instruction instruction, GenericFunction gf)
		{
			var appName = GenericFunctionHelper.GetParameterValueAsString(gf, "appName", "");			
			var goalName = GenericFunctionHelper.GetParameterValueAsString(gf, "goalName", "Start");

			if (goalName.Contains("%") || appName.Contains("%")) return (instruction, null);

			var app = goalParser.GetAllApps().Where(p => p.AppName == appName);
			var goal = app.FirstOrDefault(p => p.GoalName == goalName);
			if (goal == null)
			{
				return (null, new StepBuilderError($"Could not find {appName}/{goalName}", step, "GoalNotFound"));
			}

			SetSystem(@$"Map the user statement. 
You are provided with a <function>, you should adjust the parameters in the <function> according to <parameters>. 
Read the user statement, use your best guess to match <parameters> and modify <function> and return back

Following are the input <parameters> that should match with user statement
The parameters provided in <function> might not be correct, these are the legal parameters. Adjust <function> as needed

<parameters>
{JsonConvert.SerializeObject(goal.IncomingVariablesRequired)}
<parameters>

");
			SetAssistant(@$"

<function>
{JsonConvert.SerializeObject(gf)}
<function>
");
			var build = await base.Build(step);
			return build;
		}
		public async Task<(Instruction?, IBuilderError?)> BuilderRunGoal(GoalStep step, Instruction instruction, GenericFunction gf)
		{
			if (gf.Parameters == null || gf.Parameters.Count == 0)
			{
				return (null, new StepBuilderError("Goal name is empty", step, "GoalNotDefined", Retry: true));
			}

			var goalName = gf.GetParameter<GoalToCallInfo>("goalInfo");
			if (goalName == null)
			{
				return (null, new StepBuilderError("Goal name is empty", step, "GoalNotDefined", Retry: true));
			}

			if (goalName.Name.Contains("%"))
			{
				return (instruction, null);
			}
			

			if (allGoals == null)
			{
				allGoals = goalParser.GetAllGoals();
			}
			var goalsFound = allGoals.Where(p => p.RelativePrFolderPath.Contains(goalName.Name.AdjustPathToOs(), StringComparison.OrdinalIgnoreCase)).ToList();
			if (goalsFound.Count == 0)
			{
				return (null, new StepBuilderError($"Could not find {goalName}", step, "GoalNotFound"));
			} else if (goalsFound.Count > 1)
			{
				//todo: we should only find one goal, otherwise use should define it better
				//logger.LogWarning($"Found {goalsFound.Count} goals containing {goalName}, must be improved");
			}


			var parameters = gf.Parameters;
			if (parameters == null) return (instruction, null);

			foreach (var parameter in parameters)
			{
				memoryStack.PutForBuilder(parameter.Name, parameter.Type);
			}

			return (instruction, null);
		}

	}
}

