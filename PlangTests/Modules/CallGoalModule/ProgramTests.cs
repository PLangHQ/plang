using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using PLang.Building.Model;
using PLang.Interfaces;
using PLang.Models;
using PLang.Modules.CallGoalModule;
using PLang.Utils;
using PLangTests.Helpers;


namespace PLangTests.Modules.CallGoalModule
{
	[TestClass]
	public class ProgramTests : BasePLangTest
	{
		Program p;

		[TestInitialize]
		public void Init()
		{
			base.Initialize();
			var goal = new Goal() { RelativeAppStartupFolderPath = Path.DirectorySeparatorChar.ToString() };
			
			p = new Program(pseudoRuntime, engine, prParser, contextAccessor);
			p.Init(container, goal, null, null, null);
		}


		[TestMethod]
		public async Task RunGoalWithOnlyGoalName()
		{
			GoalToCallInfo goalToCall = new GoalToCallInfo("!Process");

			await p.RunGoal(goalToCall);

			await pseudoRuntime.Received(1).RunGoal(engine, contextAccessor, Path.DirectorySeparatorChar.ToString(), goalToCall, Arg.Any<Goal>());
		}

		[TestMethod]
		public async Task RunGoalWithOnlyAppAndGoalName_And_Parameters()
		{
			fileSystem.AddFile(Path.Join(fileSystem.RootDirectory, "apps/GoalWith1Step/.build/", ISettings.GoalFileName), PrReaderHelper.GetPrFileRaw("Start.pr"));

			prParser.ForceLoadAllGoals();

			
			var parameters = new Dictionary<string, object?>();
			parameters.Add("h", "1");
			var goalToCall = new GoalToCallInfo("!apps/GoalWith1Step", parameters);
			await p.RunGoal(goalToCall);

			await pseudoRuntime.Received(1).RunGoal(engine, contextAccessor, Path.DirectorySeparatorChar.ToString(), goalToCall, Arg.Any<Goal>());
		}


		[TestMethod]
		public async Task RunGoalWithOnlyAppAndGoalName_And_Parameters_DontWaitForResult()
		{
			var parameters = new Dictionary<string, object?>();
			parameters.Add("h", "1");
			context.AddOrReplace("test", "1");

			bool waitForExecution = false;
			var goalToCall = new GoalToCallInfo("!Process/File", parameters);
			await p.RunGoal(goalToCall, waitForExecution);			

			await pseudoRuntime.Received(1).RunGoal(engine, Arg.Any<IPLangContextAccessor>(), 
					Path.DirectorySeparatorChar.ToString(), goalToCall, Arg.Any<Goal>(), waitForExecution);
		}
	}
}
