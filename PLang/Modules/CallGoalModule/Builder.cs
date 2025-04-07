using Microsoft.CodeAnalysis.CSharp.Syntax;
using Newtonsoft.Json.Linq;
using PLang.Building.Model;
using PLang.Building.Parsers;
using PLang.Errors;
using PLang.Errors.Builder;
using PLang.Utils;

namespace PLang.Modules.CallGoalModule
{
	public class Builder : BaseBuilder
    {
		private readonly IGoalParser goalParser;
		private readonly PrParser prParser;

		public Builder(IGoalParser goalParser, PrParser prParser)
		{
			this.goalParser = goalParser;
			this.prParser = prParser;
		}

		public override async Task<(Instruction?, IBuilderError?)> Build(GoalStep step)
		{
			AppendToSystemCommand("Goals in current file is provided");
			AppendToAssistantCommand($@"
== Examples starts ==
call goal /Start => /Start is goalName that is rooted, no parameter.
call !ParseText => ParseText is goalName, no parameters
call !Gmail/Search %query%, => Gmail/Search is goalName,  %query% is key and value in parameters
call Folder/Search q=%fileName% => Folder/Search is goalName, parameter key is q, and value is %fileName%
call goal EvaluteScore %user%, write to %score% => EvaluteScore is goalName, parameter is user, return value is %score%
set %data% = ParseDocument(%document%) => ParseDocument is goalName, parameter is %document%, %data% is the return value
%user% = GetUser %id% => GetUser is goalName, parameter is %id%, return value is %user%
== Examples ends ==
");
			var goals = goalParser.ParseGoalFile(step.Goal.AbsoluteGoalPath);
			string result = string.Join(Environment.NewLine, goals.Select(g => g.GoalName));
			AppendToAssistantCommand(@$"== Goals in file == {result} == Goals in file ==");


			var build = await base.Build(step);
			if (build.BuilderError != null) return build;

			var gf = build.Instruction?.Action as GenericFunction;

			// Build should check if it's null and throw error
			if (gf != null && gf.Parameters.Count > 0)
			{
				var goalName = gf.Parameters[0].Value?.ToString();
				if (goalName != null && goalName.Contains("%"))
				{
					return build;
				}
				if (string.IsNullOrEmpty(goalName))
				{
					return (null, new BuilderError("Goal name is empty", "GoalNotDefined"));
				}
				var allGoals = goalParser.GetAllGoals();		
				var goalsFound = allGoals.Where(p => p.RelativePrFolderPath.Contains(goalName.Replace("!", "").AdjustPathToOs(), StringComparison.OrdinalIgnoreCase)).ToList();
				if (goalsFound.Count == 0)
				{
					return (null, new BuilderError($"Could not find {goalName}", "GoalNotFound"));
				}
			}
			return build;

		}

	}
}

