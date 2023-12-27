using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using PLang.Building.Model;
using PLang.Interfaces;
using PLang.Modules.CallGoalModule;
using PLang.Utils;

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
			
			p = new Program(pseudoRuntime, engine, variableHelper);
			p.Init(container, goal, null, null, memoryStack, logger, context, typeHelper, aiService, settings, null, null);
		}

		[TestMethod]
		public async Task RunGoalWithOnlyGoalName()
		{	
			await p.RunGoal("!Process");

			await pseudoRuntime.Received(1).RunGoal(engine, context, Path.DirectorySeparatorChar.ToString(), "!Process", Arg.Any<Dictionary<string, object>>(), Arg.Any<Goal>());
		}

		[TestMethod]
		public async Task RunGoalWithOnlyAppAndGoalName_And_Parameters()
		{
			var parameters = new Dictionary<string, object>();
			parameters.Add("h", "1");
			await p.RunGoal("!Process.File", parameters);

			await pseudoRuntime.Received(1).RunGoal(engine, context, Path.DirectorySeparatorChar.ToString(), "!Process.File", Arg.Is<Dictionary<string, object>>(p => p.ContainsKey("h")), Arg.Any<Goal>());
		}


		[TestMethod]
		public async Task RunGoalWithOnlyAppAndGoalName_And_Parameters_DontWaitForResult()
		{
			var parameters = new Dictionary<string, object>();
			parameters.Add("h", "1");
			context.AddOrReplace("test", "1");

			await p.RunGoal("!Process.File", parameters, false);			

			await pseudoRuntime.DidNotReceive().RunGoal(engine, context, Path.DirectorySeparatorChar.ToString(), "!Process.File", Arg.Is<Dictionary<string, object>>(p => p.ContainsKey("h")), null);
			await pseudoRuntime.Received(1).RunGoal(engine, Arg.Is<PLangAppContext>(p => p.ContainsKey("test")), Path.DirectorySeparatorChar.ToString(), "!Process.File", Arg.Is<Dictionary<string, object>>(p => p.ContainsKey("h")), Arg.Any<Goal>());
		}
	}
}
