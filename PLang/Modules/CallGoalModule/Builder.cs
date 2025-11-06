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
using System.Diagnostics;

namespace PLang.Modules.CallGoalModule
{
	public class Builder : BaseBuilder
	{
		private readonly IGoalParser goalParser;
		private readonly PrParser prParser;
		private readonly MemoryStack memoryStack;
		private readonly ILogger logger;

		public Builder(IGoalParser goalParser, PrParser prParser, IMemoryStackAccessor memoryStackAccessor, ILogger logger)
		{
			Stopwatch stopwatch = Stopwatch.StartNew();
			logger.LogDebug($"        - Start constructor for CallGoalModule.Builder - {stopwatch.ElapsedMilliseconds}");
			this.goalParser = goalParser;
			this.prParser = prParser;
			this.memoryStack = memoryStackAccessor.Current;
			this.logger = logger;
			logger.LogDebug($"        - End constructor for CallGoalModule.Builder - {stopwatch.ElapsedMilliseconds}");
		}

		public override async Task<(Instruction?, IBuilderError?)> Build(GoalStep step, IBuilderError? previousBuildError = null)
		{
			AppendToSystemCommand(@"
Only choose RunApp if user specifically defines it as app
Respect user intent on path to goal
Goals/Apps can be dynamically called, e.g. call goal HandleType%type.Name%

Use <examples> to understand user intent

<examples>
call goal /Start => function name:RunGoal  goalName:/Start, that is rooted, no parameter.
call ../ParseText => function name:RunGoal, goalName: ../ParseText, no parameters
call goal HandleType%type.Name% doStuff=1 => function: RunGoal, goalName: ""HandleType%type.Name%"", parameters is doStuff=1
run Folder/Search q=%fileName% => function: RunGoal, goalName:Folder/Search,, parameter key is q, and value is %fileName%
execute goal EvaluteScore %user%, write to %score% => function: RunGoal, goalName:EvaluteScore, parameter is user=%user%, return value is %score%
set %data% = ParseDocument(%document%) => function: RunGoal, goalName:ParseDocument, parameter is %document%, %data% is the return value
%user% = GetUser %id% => function: RunGoal, goalName:GetUser, parameter is %id%, return value is %user%
call goal Action%Type% => function: RunGoal, goalName: ""Action%Type%"", parameters is null

call app Gmail/Search %query% => function name:RunApp, appName:Gmail, goalName:/Search , %query% is key and value in parameters
call app /Builder/DbModule %content%, write to %result% => function name:RunApp, appName:Builder, goalName:/DbModule , %query% is key and value in parameters, return value %result%
<examples>
""");
			
			var build = await base.Build(step, previousBuildError);
			return build;
		}

		private static List<Goal>? allGoals = null;

		public async Task<(Instruction?, IBuilderError?)> BuilderRunGoal(GoalStep step, Instruction instruction, GenericFunction gf)
		{
			Stopwatch stopwatch = Stopwatch.StartNew();
			logger.LogDebug($"      - run BuilderRunGoal on {step.Text.MaxLength(20)} - {stopwatch.ElapsedMilliseconds}");
			if (gf.Parameters == null || gf.Parameters.Count == 0)
			{
				return (null, new StepBuilderError("Goal name is empty", step, "GoalNotDefined", Retry: true));
			}

			var goalToCall = gf.GetParameter<GoalToCallInfo>("goalInfo");
			if (goalToCall == null)
			{
				return (null, new StepBuilderError("Goal name is empty", step, "GoalNotDefined", Retry: true));
			}

			if (goalToCall.Name.Contains("%"))
			{
				return (instruction, null);
			}
			logger.LogDebug($"      - getting goals - {stopwatch.ElapsedMilliseconds}");
			var disableSystemGoals = gf.GetParameter<bool>("disableSystemGoals");
			var goals = goalParser.GetGoals();
			var systemGoals = (disableSystemGoals) ? new List<Goal>() : prParser.GetSystemGoals();

			(var goal, var error) = GoalHelper.GetGoal(step.RelativeGoalPath, step.Goal.AbsoluteAppStartupFolderPath, goalToCall, goals, systemGoals);
			if (error != null) return (instruction, new BuilderError(error));

			logger.LogDebug($"      - found goal - {stopwatch.ElapsedMilliseconds}");
			goalToCall.Path = goal.RelativePrPath;

			var parameters = gf.Parameters;
			if (parameters == null) return (instruction, null);

			foreach (var parameter in parameters)
			{
				memoryStack.PutForBuilder(parameter.Name, parameter.Type);
			}
			gf = gf.SetParameter("goalInfo", goalToCall);
			instruction = instruction with { Function = gf };

			logger.LogDebug($"      - done with BuilderRunGoal - {stopwatch.ElapsedMilliseconds}");
			return (instruction, null);
		}

	}
}

