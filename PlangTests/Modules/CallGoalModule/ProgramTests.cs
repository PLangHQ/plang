using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using PLang.Building.Model;
using PLang.Interfaces;
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
			
			p = new Program(pseudoRuntime, engine, prParser, appsRepository);
			p.Init(container, goal, null, null, memoryStack, logger, context, typeHelper, aiService, settings, null, null);
		}


		[TestMethod]
		public async Task RunGoalWithOnlyGoalName()
		{	
			await p.RunGoal("!Process");

			await pseudoRuntime.Received(1).RunGoal(engine, context, Path.DirectorySeparatorChar.ToString(), "Process", Arg.Any<Dictionary<string, object>>(), Arg.Any<Goal>());
		}

		[TestMethod]
		public async Task RunGoalWithOnlyAppAndGoalName_And_Parameters()
		{
			fileSystem.AddFile(Path.Join(fileSystem.RootDirectory, "apps/GoalWith1Step/.build/", ISettings.GoalFileName), PrReaderHelper.GetPrFileRaw("Start.pr"));

			prParser.ForceLoadAllGoals();

			var parameters = new Dictionary<string, object?>();
			parameters.Add("h", "1");
			await p.RunGoal("!apps/GoalWith1Step", parameters);

			await pseudoRuntime.Received(1).RunGoal(engine, context, Path.DirectorySeparatorChar.ToString(), "apps/GoalWith1Step", Arg.Is<Dictionary<string, object?>>(p => p.ContainsKey("h")), Arg.Any<Goal>());
		}


		[TestMethod]
		public async Task RunGoalWithOnlyAppAndGoalName_And_Parameters_DontWaitForResult()
		{
			var parameters = new Dictionary<string, object?>();
			parameters.Add("h", "1");
			context.AddOrReplace("test", "1");

			bool waitForExecution = false;

			await p.RunGoal("!Process/File", parameters, waitForExecution);			

			await pseudoRuntime.Received(1).RunGoal(engine, Arg.Is<PLangAppContext>(p => p.ContainsKey("test")), 
					Path.DirectorySeparatorChar.ToString(), "Process/File", 
					Arg.Is<Dictionary<string, object?>>(p => p.ContainsKey("h")), Arg.Any<Goal>(), waitForExecution);
		}
	}
}
