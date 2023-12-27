using PLang.Building.Model;
using PLang.Interfaces;
using PLang.Runtime;
using PLang.Utils;
using System.ComponentModel;

namespace PLang.Modules.CallGoalModule
{
	[Description("Call another Goal, when ! is prefixed, e.g. !RenameFile")]
	public class Program : BaseProgram
	{
		private readonly IPseudoRuntime pseudoRuntime;
		private readonly IEngine engine;
		private readonly VariableHelper variableHelper;

		public Program(IPseudoRuntime pseudoRuntime, IEngine engine, VariableHelper variableHelper) : base()
		{
			this.pseudoRuntime = pseudoRuntime;
			this.engine = engine;
			this.variableHelper = variableHelper;
		}

		public new Goal Goal { get; set; }
		 
		public async Task RunGoal(string goalName, Dictionary<string, object>? parameters = null, bool waitForExecution = true, int delayWhenNotWaitingInMilliseconds = 0)
		{
			if (goalName == null)
			{
				throw new Exception($"Could not find goal to call from step: {goalStep.Text}");
			}
			if (Goal == null) Goal = base.Goal;

			if (waitForExecution)
			{
				await pseudoRuntime.RunGoal(engine, context, Goal.RelativeAppStartupFolderPath, goalName, variableHelper.LoadVariables(parameters), Goal);
			} else
			{
				var newContext = new PLangAppContext();
				foreach (var item in context)
				{
					newContext.Add(item.Key, item.Value);
				}

				pseudoRuntime.RunGoal(engine, newContext, Goal.RelativeAppStartupFolderPath, goalName, variableHelper.LoadVariables(parameters), Goal);
				if (delayWhenNotWaitingInMilliseconds > 0)
				{
					await Task.Delay(delayWhenNotWaitingInMilliseconds);
				}
			}
		}


	}


}

