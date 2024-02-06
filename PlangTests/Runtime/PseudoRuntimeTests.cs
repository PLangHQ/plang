using LightInject;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using PLang.Building.Model;
using PLang.Exceptions;
using PLang.Interfaces;
using PLang.Services.OutputStream;
using PLang.Utils;
using PLangTests;
using PLangTests.Helpers;
using PLangTests.Mocks;
using System.IO.Abstractions.TestingHelpers;

namespace PLang.Runtime.Tests
{
    [TestClass()]
	public class PseudoRuntimeTests : BasePLangTest
	{
		[TestInitialize()]
		public void Init()
		{
			base.Initialize();
			SetupGoals();
		}

		private void SetupGoals()
		{
			var settings = container.GetInstance<ISettings>();
			var fileSystem = (PLangMockFileSystem) container.GetInstance<IPLangFileSystem>();	

			// Goal file that is in root app
			string GoalWith1Step = PrReaderHelper.GetPrFileRaw("GoalWith1Step.pr");
			fileSystem.AddFile(Path.Join(fileSystem.BuildPath, "GoalWith1Step", ISettings.GoalFileName), new MockFileData(GoalWith1Step));

			// Goal file that is inside the apps folder
			string GoalWith2Steps = PrReaderHelper.GetPrFileRaw("GoalWith2Steps.pr");
			fileSystem.AddFile(Path.Join(fileSystem.GoalsPath, "apps", "GoalWith2Steps", ".build", ISettings.GoalFileName), new MockFileData(GoalWith2Steps));

			prParser.ForceLoadAllGoals();

		}

		[TestMethod()]
		public async Task RunGoalTest_InternalApp()
		{
			var context = new PLangAppContext();
			context.Add("Test", 1);
			var pseudoRuntime = new PseudoRuntime(prParser, containerFactory, fileSystem);
			await pseudoRuntime.RunGoal(engine, context, @"\", "GoalWith1Step.goal", new Dictionary<string, object>());

			await engine.Received(1).RunGoal(Arg.Any<Goal>());
		}


		

		[TestMethod()]
		public async Task RunGoalTest_AppInAppsFolder_ShouldNotGetContext()
		{
			var serviceFactory = Substitute.For<IServiceContainerFactory>();
			serviceFactory.CreateContainer(context, Arg.Any<string>(), Arg.Any<string>(), Arg.Any<IOutputStream>()).Returns(p =>
			{
				var container = CreateServiceContainer();
				container.GetInstance<IEngine>().GetMemoryStack().Returns(new MemoryStack(container.GetInstance<IPseudoRuntime>(), container.GetInstance<IEngine>(), settings, context));
				return container;
			});


			var pseudoRuntime = new PseudoRuntime(prParser, serviceFactory, fileSystem);

			context.Add("Test", 1);

			engine.GetMemoryStack().Returns(new MemoryStack(pseudoRuntime, engine, settings, context));

			var parameters = new PLangAppContext();
			parameters.Add("Name", "Jim");

			
			
			await pseudoRuntime.RunGoal(engine, context, @"\", "apps/GoalWith2Steps/GoalWith2Steps", parameters);

			await engine.Received(1).RunGoal(Arg.Any<Goal>());

			engine.Received(1).GetMemoryStack();

		}

		[TestMethod()]
		[ExpectedException(typeof(GoalNotFoundException))]
		public async Task RunGoalTest_GoalNotFound()
		{
		
			var pseudoRuntime = new PseudoRuntime(prParser, containerFactory, fileSystem);

			await pseudoRuntime.RunGoal(engine, new(), @"\", "UnknownGoal.goal", new Dictionary<string, object>());
		}


		[TestMethod()]
		public async Task RunGoalTest_ParametersSetInMemoryStack()
		{
			var context = new PLangAppContext();
			context.Add("Test", 1);
			

			
			var pseudoRuntime = new PseudoRuntime(prParser, containerFactory, fileSystem);
			var memoryStackMock = Substitute.For<MemoryStack>(pseudoRuntime, engine, settings, context);

			engine.GetMemoryStack().Returns(memoryStackMock);
			var parameters = new Dictionary<string, object>
	{
		{"%Name", "Jim"},
		{"Age", 30}
	};


			await pseudoRuntime.RunGoal(engine, context, @"\", "apps/GoalWith2Steps/GoalWith2Steps", parameters);

			memoryStackMock.Received(1).Put("Name", "Jim");
			memoryStackMock.Received(1).Put("Age", 30);
		}




	}
}