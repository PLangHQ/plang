using LightInject;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using PLang.Building.Model;
using PLang.Container;
using PLang.Errors.Handlers;
using PLang.Exceptions;
using PLang.Exceptions.AskUser;
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
		PseudoRuntime pseudoRuntime;
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


			pseudoRuntime = new PseudoRuntime(fileSystem, prParser);

		}

		[TestMethod()]
		public async Task RunGoalTest_InternalApp()
		{
			var context = new PLangAppContext();
			context.AddOrReplace("Test", 1);
			engine.GetGoal("GoalWith1Step.goal").Returns(new Goal());
			await pseudoRuntime.RunGoal(engine, context, @"\", "GoalWith1Step.goal");

			await engine.Received(1).RunGoal(Arg.Any<Goal>());
		}


		

		[TestMethod()]
		public async Task RunGoalTest_AppInAppsFolder_ShouldNotGetContext()
		{
			containerFactory = Substitute.For<IServiceContainerFactory>();
			containerFactory.CreateContainer(Arg.Any<PLangAppContext>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<IOutputStreamFactory>(),
				Arg.Any<IOutputSystemStreamFactory>(), Arg.Any<IErrorHandlerFactory>(), errorSystemHandlerFactory, Arg.Any<IAskUserHandlerFactory>()).Returns(p =>
			{
				var container = CreateServiceContainer();

				IEngine engine = container.GetInstance<IEngine>();
				engine.GetMemoryStack().Returns(a =>
				{
					return new MemoryStack(pseudoRuntime, engine, settings, context);
				});
				engine.GetGoal("GoalWith2Steps").Returns(new Goal());
				return container;
			});


			
			context.AddOrReplace("Test", 1);

			engine.GetMemoryStack().Returns(new MemoryStack(pseudoRuntime, engine, settings, context));

			var parameters = new Dictionary<string, object?>();
			parameters.AddOrReplace("Name", "Jim");

			pseudoRuntime = new PseudoRuntime(fileSystem, prParser);

			await pseudoRuntime.RunGoal(engine, context, @"\", "apps/GoalWith2Steps/GoalWith2Steps");

			await engine.Received(1).RunGoal(Arg.Any<Goal>());

			engine.Received(1).GetMemoryStack();

		}

		[TestMethod()]
		public async Task RunGoalTest_GoalNotFound()
		{	

			(var e, var vars, var err) = await pseudoRuntime.RunGoal(engine, new(), @"\", "UnknownGoal.goal");
			Assert.AreEqual("No goals available", err.Message);
		}

		[TestMethod]
		public void GetAppAbsolutePath()
		{
			string absolutePathToGoal = Path.Join(fileSystem.RootDirectory, "", "apps/GoalWith2Steps/GoalWith2Steps");
			var result = pseudoRuntime.GetAppAbsolutePath(absolutePathToGoal);

			Assert.AreEqual(Path.Join(fileSystem.RootDirectory, "apps", "GoalWith2Steps"), result.absolutePath);
			Assert.AreEqual("GoalWith2Steps", result.goalName.Name);

			string absolutePathToGoalInService = Path.Join(fileSystem.RootDirectory, "", ".services/MyService/SendStuff");
			var pathToService = pseudoRuntime.GetAppAbsolutePath(absolutePathToGoalInService);

			Assert.AreEqual(Path.Join(fileSystem.RootDirectory, ".services", "MyService"), pathToService.absolutePath);
			Assert.AreEqual("SendStuff", pathToService.goalName.Name);

			string absolutePathToGoalInModule = Path.Join(fileSystem.RootDirectory, "", ".modules/MyModule");
			var pathToModule = pseudoRuntime.GetAppAbsolutePath(absolutePathToGoalInModule);

			Assert.AreEqual(Path.Join(fileSystem.RootDirectory, ".modules", "MyModule"), pathToModule.absolutePath);
			Assert.AreEqual("Start", pathToModule.goalName.Name);


			string absolutePathToGoalInModuleInApp = Path.Join(fileSystem.RootDirectory, "", ".modules/MyModule/apps/MyInternalApp/Start");
			var pathToAppInModule = pseudoRuntime.GetAppAbsolutePath(absolutePathToGoalInModuleInApp);

			Assert.AreEqual(Path.Join(fileSystem.RootDirectory, ".modules/MyModule/apps/".AdjustPathToOs(), "MyInternalApp"), pathToAppInModule.absolutePath);
			Assert.AreEqual("Start", pathToAppInModule.goalName.Name);

			string absolutePathToGoalInModuleInApp2 = Path.Join(fileSystem.RootDirectory, "", ".modules/MyModule/apps/MyInternalApp/DoStuff");
			var pathToAppInModule2 = pseudoRuntime.GetAppAbsolutePath(absolutePathToGoalInModuleInApp2);

			Assert.AreEqual(Path.Join(fileSystem.RootDirectory, ".modules/MyModule/apps/".AdjustPathToOs(), "MyInternalApp"), pathToAppInModule2.absolutePath);
			Assert.AreEqual("DoStuff", pathToAppInModule2.goalName.Name);
		}



		[TestMethod()]
		public async Task RunGoalTest_ParametersSetInMemoryStack()
		{
			var context = new PLangAppContext();
			containerFactory = Substitute.For<IServiceContainerFactory>();
			containerFactory.CreateContainer(Arg.Any<PLangAppContext>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<IOutputStreamFactory>(), 
				Arg.Any<IOutputSystemStreamFactory>(), Arg.Any<IErrorHandlerFactory>(), errorSystemHandlerFactory, Arg.Any<IAskUserHandlerFactory>()).Returns(p =>
			{
				var container = CreateServiceContainer();

				IEngine engine = container.GetInstance<IEngine>();
				engine.GetMemoryStack().Returns(a =>
				{
					return new MemoryStack(pseudoRuntime, engine, settings, context);
				});
				engine.GetGoal("GoalWith2Steps").Returns(new Goal());
				return container;
			});


			
			context.AddOrReplace("Test", 1);
			
			var memoryStackMock = Substitute.For<MemoryStack>(pseudoRuntime, engine, settings, context);

			engine.GetMemoryStack().Returns(memoryStackMock);
			var parameters = new Dictionary<string, object>
	{
		{"%Name", "Jim"},
		{"Age", 30}
	};

			pseudoRuntime = new PseudoRuntime(fileSystem, prParser);

			await pseudoRuntime.RunGoal(engine, context, @"\", "apps/GoalWith2Steps/GoalWith2Steps");

			memoryStackMock.Received(1).Put("Name", "Jim");
			memoryStackMock.Received(1).Put("Age", 30);
		}




	}
}