using LightInject;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json;
using NSubstitute;
using PLang.Building.Model;
using PLang.Interfaces;
using PLangTests;
using PLangTests.Helpers;
using PLangTests.Mocks;
using System.IO.Abstractions.TestingHelpers;

namespace PLang.Building.Events.Tests
{
	[TestClass()]
	public class EventRuntimeTests : BasePLangTest
	{
		EventRuntime eventRuntime;
		[TestInitialize]
		public void Init()
		{
			base.Initialize();
			eventRuntime = new EventRuntime(fileSystem, settings, pseudoRuntime, prParser, engine, exceptionHandlerFactory, logger);
		}

		[TestMethod()]
		public void IsGoalMatchTest_Should_Not_Match_GoalName_NotSame()
		{
			var goal = new Model.Goal();
			goal.GoalName = "TestGoal";

			var eventBinding = new EventBinding(EventType.Before, EventScope.Goal, "Start", "");

			
			var result = eventRuntime.GoalHasBinding(goal, eventBinding);
			Assert.IsFalse(result);
		}

		[TestMethod()]
		public void IsGoalMatchTest_Should_Match_GoalName_Same()
		{
			var goal = new Model.Goal();
			goal.GoalName = "Start";
			goal.Visibility = Visibility.Public;
			var eventBinding = new EventBinding(EventType.Before, EventScope.Goal, "Start", "");

			var result = eventRuntime.GoalHasBinding(goal, eventBinding);
			Assert.IsTrue(result);
		}



		[TestMethod()]
		public void IsGoalMatchTest_Should_Match_GoalName_Matches_Pattern2()
		{
			
			string goalFilePath = Path.Join(fileSystem.BuildPath, "Start", "Start.pr");
			var content = PrReaderHelper.GetPrFileRaw("Start.pr");
			fileSystem.AddFile(goalFilePath, new MockFileData(content));
			
			var goal = prParser.ParsePrFile(goalFilePath);

			var events = JsonConvert.DeserializeObject<List<EventBinding>>(eventJson);

			// Test GoalToBindTo = Start
			var result = eventRuntime.GoalHasBinding(goal, events[0]);
			Assert.IsTrue(result);

			// Test GoalToBindTo = Hello.goal
			result = eventRuntime.GoalHasBinding(goal, events[3]);
			Assert.IsTrue(result);


			goal = new Model.Goal();
			goal.GoalName = "Start";
			goal.RelativeGoalFolderPath = @"\api\";
			goal.RelativeGoalPath = @"\api\Start.goal";
			goal.Visibility = Model.Visibility.Public;
			goal.GoalFileName = @"\api\Start.goal";
			goal.AppName = @"\";
			//Test GoalToBindTo = api/*
			result = eventRuntime.GoalHasBinding(goal, events[2]);
			Assert.IsTrue(result);

			result = eventRuntime.GoalHasBinding(goal, events[1]);
			Assert.IsFalse(result);

			//Test GoalToBindTo = api/*
			goal.RelativeGoalFolderPath = "/stuff/api/dodo";
			result = eventRuntime.GoalHasBinding(goal, events[2]);
			Assert.IsFalse(result);

			//Test GoalToBindTo = *
			result = eventRuntime.GoalHasBinding(goal, events[5]);
			Assert.IsTrue(result);


			// GoalToBindTo = GenerateData(.goal)?:ProcessFile
			goal.Visibility = Model.Visibility.Private;
			goal.GoalName = "Process";
			goal.RelativeGoalPath = "/Start.goal";
			result = eventRuntime.GoalHasBinding(goal, events[4]);
			Assert.IsTrue(result);

			// GoalToBindTo = SampleApp.Hello

			fileSystem.RemoveFile(goalFilePath);

			string helloWorld = PrReaderHelper.GetPrFileRaw("HelloWorld.pr");
			fileSystem.AddFile(Path.Join(fileSystem.GoalsPath, "apps", "TestApp", ".build", ISettings.GoalFileName), new MockFileData(helloWorld));

			prParser.ForceLoadAllGoals();
			var goals = prParser.GetAllGoals();
			result = eventRuntime.GoalHasBinding(goals[0], events[1]);
			Assert.IsTrue(result);
		}

		[TestMethod()]
		public async Task RunStepEventsTest_CallEventBeforeAppStart()
		{
			// setup mocked events files
			string eventPrFile = Path.Join(fileSystem.BuildPath, "Events", ISettings.GoalFileName);
			var content = PrReaderHelper.GetPrFileRaw("Events/BeforeAppStartEvent.pr");
			fileSystem.AddFile(eventPrFile, new MockFileData(content));

			// Goal file that is in root app
			string GoalWith1Step = PrReaderHelper.GetPrFileRaw("GoalWith1Step.pr");
			fileSystem.AddFile(Path.Join(fileSystem.BuildPath, "GoalWith1Step", ISettings.GoalFileName), new MockFileData(GoalWith1Step));

			// Goal file that is inside the apps folder
			string GoalWith2Steps = PrReaderHelper.GetPrFileRaw("GoalWith2Steps.pr");
			fileSystem.AddFile(Path.Join(fileSystem.GoalsPath, "apps", "GoalWith2Steps", ".build", ISettings.GoalFileName), new MockFileData(GoalWith2Steps));

			prParser.ForceLoadAllGoals();

			// load event runtime
			await eventRuntime.Load(container);
				
			await eventRuntime.RunStartEndEvents(new(), EventType.Before, EventScope.StartOfApp);
				
			await pseudoRuntime.Received(1).RunGoal(engine, Arg.Any<PLangAppContext>(),
						@"\", "!Process", Arg.Any<Dictionary<string, object?>>());
			
		}

		[TestMethod()]
		public async Task RunStepEventsTest_CallEventAfterAppStart()
		{

			// setup mocked events files
			string eventPrFile = Path.Join(fileSystem.BuildPath, "Events", ISettings.GoalFileName);
			var content = PrReaderHelper.GetPrFileRaw("Events/AfterAppStartEvent.pr");
			fileSystem.AddFile(eventPrFile, new MockFileData(content));

			// Goal file that is in root app
			string GoalWith1Step = PrReaderHelper.GetPrFileRaw("GoalWith1Step.pr");
			fileSystem.AddFile(Path.Join(fileSystem.BuildPath, "GoalWith1Step", ISettings.GoalFileName), new MockFileData(GoalWith1Step));

			// Goal file that is inside the apps folder
			string GoalWith2Steps = PrReaderHelper.GetPrFileRaw("GoalWith2Steps.pr");
			fileSystem.AddFile(Path.Join(fileSystem.GoalsPath, "apps", "GoalWith2Steps", ".build", ISettings.GoalFileName), new MockFileData(GoalWith2Steps));

			prParser.ForceLoadAllGoals();

			// load event runtime
			await eventRuntime.Load(container);

			await eventRuntime.RunStartEndEvents(new PLangAppContext(), EventType.After, EventScope.StartOfApp);

			await pseudoRuntime.Received(1).RunGoal(engine, Arg.Any<PLangAppContext>(),
						@"\", "!Process", Arg.Any<Dictionary<string, object?>>());

		}

		[TestMethod()]
		public async Task RunStepEventsTest_CallEventOnErrorAppStart()
		{
			// setup mocked events files
			string eventPrFile = Path.Join(fileSystem.BuildPath, "Events", ISettings.GoalFileName);
			var content = PrReaderHelper.GetPrFileRaw("Events/OnErrorAppStartEvent.pr");
			fileSystem.AddFile(eventPrFile, new MockFileData(content));

			// Goal file that is in root app
			string GoalWith1Step = PrReaderHelper.GetPrFileRaw("GoalWith1Step.pr");
			fileSystem.AddFile(Path.Join(fileSystem.BuildPath, "GoalWith1Step", ISettings.GoalFileName), new MockFileData(GoalWith1Step));

			// Goal file that is inside the apps folder
			string GoalWith2Steps = PrReaderHelper.GetPrFileRaw("GoalWith2Steps.pr");
			fileSystem.AddFile(Path.Join(fileSystem.GoalsPath, "apps", "GoalWith2Steps", ".build", ISettings.GoalFileName), new MockFileData(GoalWith2Steps));

			prParser.ForceLoadAllGoals();

			// load event runtime
			await eventRuntime.Load(container);

			await eventRuntime.RunStartEndEvents(new(), EventType.OnError, EventScope.StartOfApp);

			await pseudoRuntime.Received(1).RunGoal(engine, Arg.Any<PLangAppContext>(),
						@"\", "!Process", Arg.Any<Dictionary<string, object?>>());

		}
		[TestMethod()]
		public async Task RunStepEventsTest_CallEventAppEnd()
		{

			// setup mocked events files
			string eventPrFile = Path.Join(fileSystem.BuildPath, "Events", ISettings.GoalFileName);
			var content = PrReaderHelper.GetPrFileRaw("Events/AppEndEvent.pr");
			fileSystem.AddFile(eventPrFile, new MockFileData(content));

			// Goal file that is in root app
			string GoalWith1Step = PrReaderHelper.GetPrFileRaw("GoalWith1Step.pr");
			fileSystem.AddFile(Path.Join(fileSystem.BuildPath, "GoalWith1Step", ISettings.GoalFileName), new MockFileData(GoalWith1Step));

			// Goal file that is inside the apps folder
			string GoalWith2Steps = PrReaderHelper.GetPrFileRaw("GoalWith2Steps.pr");
			fileSystem.AddFile(Path.Join(fileSystem.GoalsPath, "apps", "GoalWith2Steps", ".build", ISettings.GoalFileName), new MockFileData(GoalWith2Steps));

			prParser.ForceLoadAllGoals();

			// load event runtime
			await eventRuntime.Load(container);

			await eventRuntime.RunStartEndEvents(new(), EventType.Before, EventScope.EndOfApp);
			// test that both Before and After type works. When app ends there is no difference between Before and After
			await eventRuntime.RunStartEndEvents(new(), EventType.After, EventScope.EndOfApp);

			await pseudoRuntime.Received(2).RunGoal(engine, Arg.Any<PLangAppContext>(),
						@"\", "!Process", Arg.Any<Dictionary<string, object?>>());

		}

		[TestMethod()]
		public async Task RunStepEventsTest_CallEventOnErrorOnApp()
		{
			// setup mocked events files
			string eventPrFile = Path.Join(fileSystem.BuildPath, "Events", ISettings.GoalFileName);
			var content = PrReaderHelper.GetPrFileRaw("Events/OnErrorAppRunningEvent.pr");
			fileSystem.AddFile(eventPrFile, new MockFileData(content));

			// Goal file that is in root app
			string GoalWith1Step = PrReaderHelper.GetPrFileRaw("GoalWith1Step.pr");
			fileSystem.AddFile(Path.Join(fileSystem.BuildPath, "GoalWith1Step", ISettings.GoalFileName), new MockFileData(GoalWith1Step));

			// Goal file that is inside the apps folder
			string GoalWith2Steps = PrReaderHelper.GetPrFileRaw("GoalWith2Steps.pr");
			fileSystem.AddFile(Path.Join(fileSystem.GoalsPath, "apps", "GoalWith2Steps", ".build", ISettings.GoalFileName), new MockFileData(GoalWith2Steps));

			prParser.ForceLoadAllGoals();

			// load event runtime
			await eventRuntime.Load(container);

			await eventRuntime.RunStartEndEvents(new(), EventType.OnError, EventScope.RunningApp);

			await pseudoRuntime.Received(1).RunGoal(engine, Arg.Any<PLangAppContext>(),
						@"\", "!Process", Arg.Any<Dictionary<string, object?>>());

		}

		[TestMethod()]
		public async Task RunStepEventsTest_CallEventBeforeGoalHasRun()
		{

			// setup mocked events files
			string eventPrFile = Path.Join(fileSystem.BuildPath, "Events", ISettings.GoalFileName);
			var content = PrReaderHelper.GetPrFileRaw("Events/BeforeGoalEvent.pr");
			fileSystem.AddFile(eventPrFile, new MockFileData(content));

			// Goal file that is in root app
			string GoalWith1Step = PrReaderHelper.GetPrFileRaw("GoalWith1Step.pr");
			fileSystem.AddFile(Path.Join(fileSystem.BuildPath, "GoalWith1Step", ISettings.GoalFileName), new MockFileData(GoalWith1Step));

			// Goal file that is inside the apps folder
			string GoalWith2Steps = PrReaderHelper.GetPrFileRaw("GoalWith2Steps.pr");
			fileSystem.AddFile(Path.Join(fileSystem.GoalsPath, "apps", "GoalWith2Steps", ".build", ISettings.GoalFileName), new MockFileData(GoalWith2Steps));

			prParser.ForceLoadAllGoals();

			// load event runtime
			await eventRuntime.Load(container);

			var goals = prParser.GetAllGoals().Where(p => p.GoalFileName != "Events.goal").ToList();
			foreach (var goal in goals)
			{

				await eventRuntime.RunGoalEvents(new(), EventType.Before, goal);

				await pseudoRuntime.Received(1).RunGoal(engine, Arg.Any<PLangAppContext>(),
							goal.RelativeAppStartupFolderPath, "!Process", Arg.Any<Dictionary<string, object?>>(), Arg.Any<Goal>());
			}
		}

		[TestMethod()]
		public async Task RunStepEventsTest_CallEventAfterGoalHasRun()
		{

			// setup mocked events files
			string eventPrFile = Path.Join(fileSystem.BuildPath, "Events", ISettings.GoalFileName);
			var content = PrReaderHelper.GetPrFileRaw("Events/AfterGoalEvent.pr");
			fileSystem.AddFile(eventPrFile, new MockFileData(content));

			// Goal file that is in root app
			string GoalWith1Step = PrReaderHelper.GetPrFileRaw("GoalWith1Step.pr");
			fileSystem.AddFile(Path.Join(fileSystem.BuildPath, "GoalWith1Step", ISettings.GoalFileName), new MockFileData(GoalWith1Step));

			// Goal file that is inside the apps folder
			string GoalWith2Steps = PrReaderHelper.GetPrFileRaw("GoalWith2Steps.pr");
			fileSystem.AddFile(Path.Join(fileSystem.GoalsPath, "apps", "GoalWith2Steps", ".build", ISettings.GoalFileName), new MockFileData(GoalWith2Steps));

			prParser.ForceLoadAllGoals();

			// load event runtime
			await eventRuntime.Load(container);

			var goals = prParser.GetAllGoals().Where(p => p.GoalFileName != "Events.goal").ToList();
			foreach (var goal in goals)
			{

				await eventRuntime.RunGoalEvents(new(), EventType.After, goal);

				await pseudoRuntime.Received(1).RunGoal(engine, Arg.Any<PLangAppContext>(),
							goal.RelativeAppStartupFolderPath, "!Process", Arg.Any<Dictionary<string, object?>>(), Arg.Any<Goal>());
			}
		}

		[TestMethod()]
		public async Task RunStepEventsTest_CallEventBeforeStepHasRun()
		{
			// setup mocked events files
			string eventPrFile = Path.Join(fileSystem.BuildPath, "Events", ISettings.GoalFileName);
			var content = PrReaderHelper.GetPrFileRaw("Events/BeforeStepEvent.pr");
			fileSystem.AddFile(eventPrFile, new MockFileData(content));

			// Goal file that is in root app
			string GoalWith1Step = PrReaderHelper.GetPrFileRaw("GoalWith1Step.pr");
			fileSystem.AddFile(Path.Join(fileSystem.BuildPath, "GoalWith1Step", ISettings.GoalFileName), new MockFileData(GoalWith1Step));

			// Goal file that is inside the apps folder
			string GoalWith2Steps = PrReaderHelper.GetPrFileRaw("GoalWith2Steps.pr");
			fileSystem.AddFile(Path.Join(fileSystem.GoalsPath, "apps", "GoalWith2Steps", ".build", ISettings.GoalFileName), new MockFileData(GoalWith2Steps));

			prParser.ForceLoadAllGoals();

			// load event runtime
			await eventRuntime.Load(container);

			var goals = prParser.GetAllGoals().Where(p => p.GoalFileName != "Events.goal").ToList();
			foreach (var goal in goals)
			{
				foreach (var step in goal.GoalSteps)
				{
					await eventRuntime.RunStepEvents(new(), EventType.Before, goal, step);
				}
				await pseudoRuntime.Received(goal.GoalSteps.Count).RunGoal(engine, Arg.Any<PLangAppContext>(),
							goal.RelativeAppStartupFolderPath, "!Process", Arg.Any<Dictionary<string, object?>>(), Arg.Any<Goal>());
			}
		}


		[TestMethod()]
		public async Task RunStepEventsTest_CallEventAfterStepHasRun()
		{
			// setup mocked events files
			string eventPrFile = Path.Join(fileSystem.BuildPath, "Events", ISettings.GoalFileName);
			var content = PrReaderHelper.GetPrFileRaw("Events/AfterStepEvent.pr");
			fileSystem.AddFile(eventPrFile, new MockFileData(content));

			// Goal file that is in root app
			string GoalWith1Step = PrReaderHelper.GetPrFileRaw("GoalWith1Step.pr");
			fileSystem.AddFile(Path.Join(fileSystem.BuildPath, "GoalWith1Step", ISettings.GoalFileName), new MockFileData(GoalWith1Step));

			// Goal file that is inside the apps folder
			string GoalWith2Steps = PrReaderHelper.GetPrFileRaw("GoalWith2Steps.pr");
			fileSystem.AddFile(Path.Join(fileSystem.GoalsPath, "apps", "GoalWith2Steps", ".build", ISettings.GoalFileName), new MockFileData(GoalWith2Steps));

			prParser.ForceLoadAllGoals();

			// load event runtime
			await eventRuntime.Load(container);

			var goals = prParser.GetAllGoals().Where(p => p.GoalFileName != "Events.goal").ToList();
			foreach (var goal in goals)
			{
				foreach (var step in goal.GoalSteps)
				{
					await eventRuntime.RunStepEvents(new(), EventType.After, goal, step);
				}
				await pseudoRuntime.Received(goal.GoalSteps.Count).RunGoal(engine, Arg.Any<PLangAppContext>(),
							goal.RelativeAppStartupFolderPath, "!Process", Arg.Any<Dictionary<string, object?>>(), Arg.Any<Goal>());
			}
		}


		[TestMethod()]
		public async Task RunStepEventsTest_CallEventOnGoalError()
		{

			// setup mocked events files
			string eventPrFile = Path.Join(fileSystem.BuildPath, "Events", ISettings.GoalFileName);
			var content = PrReaderHelper.GetPrFileRaw("Events/OnErrorGoalEvent.pr");
			fileSystem.AddFile(eventPrFile, new MockFileData(content));

			// Goal file that is in root app
			string GoalWith1Step = PrReaderHelper.GetPrFileRaw("GoalWith1Step.pr");
			fileSystem.AddFile(Path.Join(fileSystem.BuildPath, "GoalWith1Step", ISettings.GoalFileName), new MockFileData(GoalWith1Step));

			// Goal file that is inside the apps folder
			string GoalWith2Steps = PrReaderHelper.GetPrFileRaw("GoalWith2Steps.pr");
			fileSystem.AddFile(Path.Join(fileSystem.GoalsPath, "apps", "GoalWith2Steps", ".build", ISettings.GoalFileName), new MockFileData(GoalWith2Steps));

			prParser.ForceLoadAllGoals();

			// load event runtime
			await eventRuntime.Load(container);

			var goals = prParser.GetAllGoals().Where(p => p.GoalFileName != "Events.goal").ToList();
			foreach (var goal in goals)
			{
				await eventRuntime.RunGoalEvents(new(), EventType.OnError, goal);
				await pseudoRuntime.Received(1).RunGoal(engine, Arg.Any<PLangAppContext>(),
							goal.RelativeAppStartupFolderPath, "!Process", Arg.Any<Dictionary<string, object?>>(), Arg.Any<Goal>());
			}
		}


		[TestMethod()]
		public async Task RunStepEventsTest_CallEventOnStepError()
		{
			// setup mocked events files
			string eventPrFile = Path.Join(fileSystem.BuildPath, "Events", ISettings.GoalFileName);
			var content = PrReaderHelper.GetPrFileRaw("Events/OnErrorStepEvent.pr");
			fileSystem.AddFile(eventPrFile, new MockFileData(content));

			// Goal file that is in root app
			string GoalWith1Step = PrReaderHelper.GetPrFileRaw("GoalWith1Step.pr");
			fileSystem.AddFile(Path.Join(fileSystem.BuildPath, "GoalWith1Step", ISettings.GoalFileName), new MockFileData(GoalWith1Step));

			// Goal file that is inside the apps folder
			string GoalWith2Steps = PrReaderHelper.GetPrFileRaw("GoalWith2Steps.pr");
			fileSystem.AddFile(Path.Join(fileSystem.GoalsPath, "apps", "GoalWith2Steps", ".build", ISettings.GoalFileName), new MockFileData(GoalWith2Steps));

			prParser.ForceLoadAllGoals();

			// load event runtime
			await eventRuntime.Load(container);

			var goals = prParser.GetAllGoals().Where(p => p.GoalFileName != "Events.goal").ToList();
			foreach (var goal in goals)
			{
				foreach (var step in goal.GoalSteps)
				{
					await eventRuntime.RunStepEvents(new(), EventType.OnError, goal, step);
				}
				await pseudoRuntime.Received(goal.GoalSteps.Count).RunGoal(engine, Arg.Any<PLangAppContext>(),
							goal.RelativeAppStartupFolderPath, "!Process", Arg.Any<Dictionary<string, object?>>(), Arg.Any<Goal>());
			}
		}

		[TestMethod()]
		public void GetRuntimeEventsFilesTest_MakeSureRootEventsIsLast()
		{

			fileSystem.AddFile(Path.Join(fileSystem.BuildPath, "Events", ISettings.GoalFileName), new MockFileData(""));
			fileSystem.AddFile(Path.Join(fileSystem.GoalsPath, "apps", "TestApp", ".build", "Events", ISettings.GoalFileName), new MockFileData(""));
			fileSystem.AddFile(Path.Join(fileSystem.GoalsPath, "apps", "HelloWorld", ".build", "Events", ISettings.GoalFileName), new MockFileData(""));
			fileSystem.AddFile(Path.Join(fileSystem.GoalsPath, "apps", "HelloWorld", ".build", "Process", ISettings.GoalFileName), new MockFileData(""));
			fileSystem.AddFile(Path.Join(fileSystem.GoalsPath, "HelloWorld", ISettings.GoalFileName), new MockFileData(""));

			var eventFiles = eventRuntime.GetRuntimeEventsFiles(fileSystem.GoalsPath, "Events");

			Assert.AreEqual(3, eventFiles.Count);
			Assert.AreEqual(Path.Join(fileSystem.BuildPath, "Events", ISettings.GoalFileName), eventFiles[0]);

		}

		[TestMethod()]
		public void GetRuntimeEventsFilesTest_MakeSureRootBuilderEventsIsLast()
		{

			fileSystem.AddFile(Path.Join(fileSystem.BuildPath, "BuilderEvents", ISettings.GoalFileName), new MockFileData(""));
			fileSystem.AddFile(Path.Join(fileSystem.GoalsPath, "apps", "TestApp", ".build", "BuilderEvents", ISettings.GoalFileName), new MockFileData(""));
			fileSystem.AddFile(Path.Join(fileSystem.GoalsPath, "apps", "HelloWorld", ".build", "Events", ISettings.GoalFileName), new MockFileData(""));
			fileSystem.AddFile(Path.Join(fileSystem.GoalsPath, "apps", "HelloWorld", ".build", "Process", ISettings.GoalFileName), new MockFileData(""));
			fileSystem.AddFile(Path.Join(fileSystem.GoalsPath, "HelloWorld", ISettings.GoalFileName), new MockFileData(""));

			var eventFiles = eventRuntime.GetRuntimeEventsFiles(fileSystem.GoalsPath, "BuilderEvents");

			Assert.AreEqual(2, eventFiles.Count);
			Assert.AreEqual(Path.Join(fileSystem.BuildPath, "BuilderEvents", ISettings.GoalFileName), eventFiles[0]);

		}

		[TestMethod]
		public async Task LoadEvents()
		{

			var content = PrReaderHelper.GetPrFileRaw("Events/Events.pr");
			fileSystem.AddFile(Path.Join(fileSystem.BuildPath, "Events", ISettings.GoalFileName), new MockFileData(content));
			prParser.ForceLoadAllGoals();

			await eventRuntime.Load(container);
			var events = await eventRuntime.GetRuntimeEvents();

			Assert.AreEqual(3, events.Count);
			Assert.AreEqual(EventType.Before, events[0].EventType);
			Assert.AreEqual(EventScope.StartOfApp, events[0].EventScope);
			Assert.AreEqual("!Process", events[0].GoalToCall);
			Assert.AreEqual(null, events[0].GoalToBindTo);

			Assert.AreEqual(EventType.After, events[1].EventType);
			Assert.AreEqual(EventScope.Step, events[1].EventScope);
			Assert.AreEqual("!Process", events[1].GoalToCall);
			Assert.AreEqual("*", events[1].GoalToBindTo);
		}


		string eventJson = @"[
  {
    ""EventType"": 1,
    ""EventScope"": 30,
    ""GoalToBindTo"": ""Start"",
    ""GoalToCall"": ""!SendEmail"",
    ""StepText"": """",
    ""IncludePrivate"": false
  },{
    ""EventType"": 1,
    ""EventScope"": 30,
    ""GoalToBindTo"": ""TestApp.HelloWorld"",
    ""GoalToCall"": ""!SendEmail"",
    ""StepText"": ""write to db"",
    ""IncludePrivate"": false
  },
  {
    ""EventType"": 0,
    ""EventScope"": 20,
    ""GoalToBindTo"": ""api/*"",
    ""GoalToCall"": ""!DoStuff"",
    ""IncludePrivate"": true
  },
  {
    ""EventType"": 0,
    ""EventScope"": 30,
    ""GoalToBindTo"": ""Start.goal"",
    ""GoalToCall"": ""!TestApp.HelloWorld""
  },
  {
    ""EventType"": 0,
    ""EventScope"": 0,
    ""GoalToBindTo"": ""Start:Process"",
    ""GoalToCall"": ""!BeforeStart"",
    ""IncludePrivate"": true
  },
  {
    ""EventType"": 0,
    ""EventScope"": 1,
    ""GoalToBindTo"": ""*"",
    ""GoalToCall"": ""!SendEmail""
  }
]";

	}
}