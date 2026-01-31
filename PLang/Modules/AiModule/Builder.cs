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

namespace PLang.Modules.AiModule
{
	public class Builder : BaseBuilder
	{
		private readonly IEngine engine;
		private readonly IGoalParser goalParser;
		private readonly ILogger logger;

		public Builder(IEngine engine, IGoalParser goalParser, IPrParser prParser, IMemoryStackAccessor memoryStackAccessor, ILogger logger)
		{
			Stopwatch stopwatch = Stopwatch.StartNew();
			logger.LogDebug($"        - Start constructor for CallGoalModule.Builder - {stopwatch.ElapsedMilliseconds}");
			this.engine = engine;
			this.goalParser = goalParser;
			this.logger = logger;
			logger.LogDebug($"        - End constructor for CallGoalModule.Builder - {stopwatch.ElapsedMilliseconds}");
		}

		public record SelectedApp(string Name, string RelativePath);
		public record SelectedGoal(string Name, string RelativePath);
		public override async Task<(Instruction?, IBuilderError?)> Build(GoalStep step, IBuilderError? previousBuildError = null)
		{
			var goalToCall = new GoalToCallInfo("/modules/AiModule/Builder")
			{
				Parameters = new() { ["step"] = step, ["error"] = previousBuildError }
			};

			var result = await engine.RunGoal(goalToCall, step.Goal, context);
			return (null, null);
		}

		public record StepCompiled(string stepText, Dictionary<string, object?> Parameters, List<string> Returns);


		public async Task<(Instruction?, IBuilderError?)> BuilderRunApp(GoalStep step, Instruction instruction, GenericFunction gf)
		{

			//todo: need to fix this valiation
			return (instruction, null);

		}

	}
}

