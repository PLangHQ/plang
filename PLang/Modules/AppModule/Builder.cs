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

namespace PLang.Modules.AppModule
{
	public class Builder : BaseBuilder
	{
		private readonly IGoalParser goalParser;
		private readonly ILogger logger;

		public Builder(IGoalParser goalParser, PrParser prParser, IMemoryStackAccessor memoryStackAccessor, ILogger logger)
		{
			Stopwatch stopwatch = Stopwatch.StartNew();
			logger.LogDebug($"        - Start constructor for CallGoalModule.Builder - {stopwatch.ElapsedMilliseconds}");
			this.goalParser = goalParser;
			this.logger = logger;
			logger.LogDebug($"        - End constructor for CallGoalModule.Builder - {stopwatch.ElapsedMilliseconds}");
		}

		public record SelectedApp(string Name, string RelativePath);
		public record SelectedGoal(string Name, string RelativePath);
		public override async Task<(Instruction?, IBuilderError?)> Build(GoalStep step, IBuilderError? previousBuildError = null)
		{
			var apps = goalParser.GetApps();

			string system = @$"Give me relative path to app that matches user intent from the <apps> selection
<apps>
{apps.Select(p => new { p.Name, p.RelativePath }).ToList().ToJson()}
</apps>
";
			var (selectedApp, error) = await base.LlmRequest<SelectedApp>(system, step);
			if (error != null) return (null, error);

			var app = apps.FirstOrDefault(p => p.RelativePath == selectedApp.RelativePath);
			if (app == null)
			{
				return (null, new StepBuilderError($"relative app path: '{selectedApp.RelativePath}' could not be found. Try again.", step));
			}

			var goals = app.Goals.Where(p => p.Visibility == Visibility.Public)
				.Select(p =>
				{
					return new { p.GoalName, p.RelativeGoalPath, p.Description };
				}).ToList();
			system = $@"
Map user intent to a goal that fits best from <goals>, make sure to map parameters correctly

<goals>
{goals.ToJson()}
</goals>
";
			(var selectedGoal, error) = await LlmRequest<SelectedGoal>(system, step);
			if (selectedGoal == null)
			{
				return (null, new StepBuilderError($"Could not select a goal. This is the system:\n{system}", step, Retry: false));
			}

			var goal = app.Goals.FirstOrDefault(p => p.RelativeGoalPath == selectedGoal.RelativePath);
			if (goal == null)
			{
				return (null, new StepBuilderError($"relative goal path: '{selectedGoal.RelativePath}' could not be found. Try again.", step));
			}

			system = $@"formalize the user input and rewrite the step text so that parameters matches %variable% in the <goal>

The step text should contain name of goal and well defined parameters(optional) and return values(optional)

formalize pattern: GoalName(key=value, ...) -> %returnValue%

ONLY use %variables% defined by user, NO OTHER
the user input might declare a %variable% to write into, set that as the return object
%variables% defined by user, should match goal %variable%, e.g. user: %name% => parameters: {{""name"":""%name%""}}

Goal is a code that will run

<goal>
{goal.Text}
</goal>
";
			(var stepCompiled, error) = await LlmRequest<StepCompiled>(system, step);


			var build = await base.Build(step, previousBuildError);
			return build;
		}

		public record StepCompiled(string stepText, Dictionary<string, object?> Parameters, List<string> Returns);


		public async Task<(Instruction?, IBuilderError?)> BuilderRunApp(GoalStep step, Instruction instruction, GenericFunction gf)
		{

			//todo: need to fix this valiation
			return (instruction, null);

		}

	}
}

