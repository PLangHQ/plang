using Microsoft.CodeAnalysis.CSharp.Syntax;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using PLang.Building.Model;
using PLang.Building.Parsers;
using PLang.Errors;
using PLang.Errors.Builder;
using PLang.Runtime;
using PLang.Utils;

namespace PLang.Modules.CallGoalModule
{
	public class Builder : BaseBuilder
	{
		private readonly IGoalParser goalParser;
		private readonly PrParser prParser;
		private readonly MemoryStack memoryStack;

		public Builder(IGoalParser goalParser, PrParser prParser, MemoryStack memoryStack)
		{
			this.goalParser = goalParser;
			this.prParser = prParser;
			this.memoryStack = memoryStack;
		}

		public override async Task<(Instruction?, IBuilderError?)> Build(GoalStep step)
		{
			AppendToSystemCommand(@"Match the one of the <goals_in_file>, <goals> or <apps> depending on user intent. In that order
Use <examples> to understand user input

<examples>
call goal /Start => /Start is goalName that is rooted, no parameter.
call ParseText => ParseText is goalName, no parameters
call Gmail/Search %query%, => Gmail/Search is goalName,  %query% is key and value in parameters
call Folder/Search q=%fileName% => Folder/Search is goalName, parameter key is q, and value is %fileName%
call goal EvaluteScore %user%, write to %score% => EvaluteScore is goalName, parameter is user, return value is %score%
set %data% = ParseDocument(%document%) => ParseDocument is goalName, parameter is %document%, %data% is the return value
%user% = GetUser %id% => GetUser is goalName, parameter is %id%, return value is %user%
<examples>
");
			string goalsInFile = "";
			var goals = goalParser.ParseGoalFile(step.Goal.AbsoluteGoalPath).Where(p => p.RelativePrFolderPath != step.Goal.RelativePrFolderPath).ToList();
			if (goals.Count > 0)
			{
				goalsInFile = JsonConvert.SerializeObject(goals.Select(g => new { g.GoalName, g.RelativePrFolderPath, g.Description }));
			}

			AppendToAssistantCommand($"<goals_in_file>\n{goalsInFile}\n<goals_in_file>");
			/*
			string goalsInApp = "";
			goals = goalParser.GetAllGoals().OrderBy(p => !p.IsOS && p.Visibility == Visibility.Public).ToList();
			if (goals.Count > 0)
			{
				goalsInApp = JsonConvert.SerializeObject(goals.Select(g => new { g.GoalName, g.RelativePrFolderPath, g.Description }));
			}
			AppendToAssistantCommand($"<goals>\n{goalsInApp}\n<goals>");
			*/
			string appsStr = "";
			var apps = goalParser.GetAllApps().OrderBy(p => p.Visibility == Visibility.Public).ToList();
			if (apps.Count > 0)
			{
				var groupedApps = apps.GroupBy(p => p.AbsoluteAppStartupFolderPath);
				List<object> appsToSerialize = new();
				foreach (var groupedApp in groupedApps)
				{
					var firstGoal = groupedApp.FirstOrDefault(p => p.GoalName == "Start") ?? groupedApp.FirstOrDefault();
					var subGoals = groupedApp.Select(p => new { p.GoalName, p.RelativePrFolderPath });
					var app = new
					{
						AppName = firstGoal.AppName,
						firstGoal.Description,
						SubGoals = subGoals
					};
					appsToSerialize.Add(app);
				}
				appsStr = JsonConvert.SerializeObject(appsToSerialize);
			}
			AppendToAssistantCommand($"<apps>\n{appsStr}\n<apps>");


			var build = await base.Build(step);
			if (build.BuilderError != null) return build;

			var gf = build.Instruction?.Action as GenericFunction;
			if (gf != null && gf.FunctionName.Equals("RunApp"))
			{
				var appName = gf.Parameters[0].Value?.ToString();
				var app = goalParser.GetAllApps().Where(p => p.AppName == appName);
				var goalName = gf.Parameters[1].Value?.ToString();
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
				build = await base.Build(step);
				

			} else	if (gf != null && gf.Parameters.Count > 0)
			{
				var goalName = gf.Parameters[0].Value?.ToString();
				if (goalName != null && goalName.Contains("%"))
				{
					return build;
				}
				if (string.IsNullOrEmpty(goalName))
				{
					return (null, new StepBuilderError("Goal name is empty", step, "GoalNotDefined", Retry: true));
				}

				if (allGoals == null)
				{
					allGoals = goalParser.GetAllGoals();
				}
				var goalsFound = allGoals.Where(p => p.RelativePrFolderPath.Contains(goalName.Replace("!", "").AdjustPathToOs(), StringComparison.OrdinalIgnoreCase)).ToList();
				if (goalsFound.Count == 0)
				{
					return (null, new StepBuilderError($"Could not find {goalName}", step, "GoalNotFound"));
				}
			}
			return build;

		}

		private static List<Goal>? allGoals = null;

		public async Task BuilderRunGoal(GenericFunction gf, GoalStep step)
		{

			var parameters = GeneralFunctionHelper.GetParameterValueAsDictionary(gf, "parameters");
			if (parameters == null) return;

			foreach (var parameter in parameters)
			{
				memoryStack.PutForBuilder(parameter.Key, parameter.Value);
			}

		}

	}
}

