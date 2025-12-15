using LightInject;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json;
using NSubstitute;
using PLang.Building.Model;
using PLang.Events;
using PLang.Interfaces;
using PLangTests;
using PLangTests.Helpers;
using PLangTests.Mocks;
using System.IO.Abstractions.TestingHelpers;
using PLang.Errors;
using Assert = Microsoft.VisualStudio.TestTools.UnitTesting.Assert;

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
			eventRuntime = new EventRuntime(fileSystem, prParser, appContext, contextAccessor, logger, null, null);
		}

		[TestMethod()]
		public void IsGoalMatchTest_Should_Not_Match_GoalName_NotSame()
		{
			var goal = new Model.Goal();
			goal.GoalName = "TestGoal";

			var eventBinding = new EventBinding(EventType.Before, EventScope.Goal, "Start", "Start");

			
			var result = eventRuntime.GoalHasBinding(goal, eventBinding);
			Assert.IsFalse(result);
		}

		[TestMethod()]
		public void IsGoalMatchTest_Should_Match_GoalName_Same()
		{
			var goal = new Model.Goal();
			goal.GoalName = "Start";
			goal.Visibility = Visibility.Public;
			var eventBinding = new EventBinding(EventType.Before, EventScope.Goal, "Start", "Start2");

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
			string eventPrFile = Path.Join(fileSystem.BuildPath, "events", ISettings.GoalFileName);
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
			eventRuntime.Load();
				
			await eventRuntime.RunStartEndEvents(EventType.Before, EventScope.StartOfApp, Goal.NotFound);
				
			await pseudoRuntime.Received(1).RunGoal(engine, Arg.Any<IPLangContextAccessor>(),
						@"\", new Models.GoalToCallInfo("Process"));
			
		}

		[TestMethod()]
		public async Task RunStepEventsTest_CallEventAfterAppStart()
		{

			// setup mocked events files
			string eventPrFile = Path.Join(fileSystem.BuildPath, "events", ISettings.GoalFileName);
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
			eventRuntime.Load();

			await eventRuntime.RunStartEndEvents(EventType.After, EventScope.StartOfApp, Goal.NotFound);

			await pseudoRuntime.Received(1).RunGoal(engine, Arg.Any<IPLangContextAccessor>(),
						@"\", "!Process");

		}

		[TestMethod()]
		public async Task RunStepEventsTest_CallEventOnErrorAppStart()
		{
			// setup mocked events files
			string eventPrFile = Path.Join(fileSystem.BuildPath, "events", ISettings.GoalFileName);
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
			eventRuntime.Load();

			await eventRuntime.RunStartEndEvents(EventType.Before, EventScope.AppError, Goal.NotFound);

			await pseudoRuntime.Received(1).RunGoal(engine, Arg.Any<IPLangContextAccessor>(),
						@"\", "!Process");

		}
		[TestMethod()]
		public async Task RunStepEventsTest_CallEventAppEnd()
		{

			// setup mocked events files
			string eventPrFile = Path.Join(fileSystem.BuildPath, "events", ISettings.GoalFileName);
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
			eventRuntime.Load();

			await eventRuntime.RunStartEndEvents(EventType.Before, EventScope.EndOfApp, Goal.NotFound);
			// test that both Before and After type works. When app ends there is no difference between Before and After
			await eventRuntime.RunStartEndEvents(EventType.After, EventScope.EndOfApp, Goal.NotFound);

			await pseudoRuntime.Received(2).RunGoal(engine, Arg.Any<IPLangContextAccessor>(),
						@"\", "!Process");

		}

		[TestMethod()]
		public async Task RunStepEventsTest_CallEventOnErrorOnApp()
		{
			// setup mocked events files
			string eventPrFile = Path.Join(fileSystem.BuildPath, "events", ISettings.GoalFileName);
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
			eventRuntime.Load();

			await eventRuntime.RunStartEndEvents(EventType.After, EventScope.AppError, Goal.NotFound);

			await pseudoRuntime.Received(1).RunGoal(engine, Arg.Any<IPLangContextAccessor>(),
						@"\", "!Process");

		}

		[TestMethod()]
		public async Task RunStepEventsTest_CallEventBeforeGoalHasRun()
		{

			// setup mocked events files
			string eventPrFile = Path.Join(fileSystem.BuildPath, "events", ISettings.GoalFileName);
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
			eventRuntime.Load();

			var goals = prParser.GetAllGoals().Where(p => p.GoalFileName != "Events.goal").ToList();
			foreach (var goal in goals)
			{

				await eventRuntime.RunGoalEvents(EventType.Before, goal);

				await pseudoRuntime.Received(1).RunGoal(engine, Arg.Any<IPLangContextAccessor>(),
							goal.RelativeAppStartupFolderPath, "!Process", Arg.Any<Goal>());
			}
		}

		[TestMethod()]
		public async Task RunStepEventsTest_CallEventAfterGoalHasRun()
		{

			// setup mocked events files
			string eventPrFile = Path.Join(fileSystem.BuildPath, "events", ISettings.GoalFileName);
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
			eventRuntime.Load();

			var goals = prParser.GetAllGoals().Where(p => p.GoalFileName != "Events.goal").ToList();
			foreach (var goal in goals)
			{

				await eventRuntime.RunGoalEvents(EventType.After, goal);

				await pseudoRuntime.Received(1).RunGoal(engine, Arg.Any<IPLangContextAccessor>(),
							goal.RelativeAppStartupFolderPath, "!Process", Arg.Any<Goal>());
			}
		}

		[TestMethod()]
		public async Task RunStepEventsTest_CallEventBeforeStepHasRun()
		{
			// setup mocked events files
			string eventPrFile = Path.Join(fileSystem.BuildPath, "events", ISettings.GoalFileName);
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
			eventRuntime.Load();

			var goals = prParser.GetAllGoals().Where(p => p.GoalFileName != "Events.goal").ToList();
			foreach (var goal in goals)
			{
				foreach (var step in goal.GoalSteps)
				{
					await eventRuntime.RunStepEvents(EventType.Before, goal, step);
				}
				await pseudoRuntime.Received(goal.GoalSteps.Count).RunGoal(engine, Arg.Any<IPLangContextAccessor>(),
							goal.RelativeAppStartupFolderPath, "!Process", Arg.Any<Goal>());
			}
		}


		[TestMethod()]
		public async Task RunStepEventsTest_CallEventAfterStepHasRun()
		{
			// setup mocked events files
			string eventPrFile = Path.Join(fileSystem.BuildPath, "events", ISettings.GoalFileName);
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
			eventRuntime.Load();

			var goals = prParser.GetAllGoals().Where(p => p.GoalFileName != "Events.goal").ToList();
			foreach (var goal in goals)
			{
				foreach (var step in goal.GoalSteps)
				{
					await eventRuntime.RunStepEvents(EventType.After, goal, step);
				}
				await pseudoRuntime.Received(goal.GoalSteps.Count).RunGoal(engine, Arg.Any<IPLangContextAccessor>(),
							goal.RelativeAppStartupFolderPath, "!Process", Arg.Any<Goal>());
			}
		}


		[TestMethod()]
		public async Task RunStepEventsTest_CallEventOnGoalError()
		{

			// setup mocked events files
			string eventPrFile = Path.Join(fileSystem.BuildPath, "events", ISettings.GoalFileName);
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
			eventRuntime.Load();

			var goals = prParser.GetAllGoals().Where(p => p.GoalFileName != "Events.goal").ToList();
			foreach (var goal in goals)
			{
				await eventRuntime.RunGoalErrorEvents(goal, 0, new Error("Test"));
				await pseudoRuntime.Received(1).RunGoal(engine, Arg.Any<IPLangContextAccessor>(),
							goal.RelativeAppStartupFolderPath, "!Process", Arg.Any<Goal>());
			}
		}


		[TestMethod()]
		public async Task RunStepEventsTest_CallEventOnStepError()
		{
			// setup mocked events files
			string eventPrFile = Path.Join(fileSystem.BuildPath, "events", ISettings.GoalFileName);
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
			eventRuntime.Load();

			var goals = prParser.GetAllGoals().Where(p => p.GoalFileName != "Events.goal").ToList();
			foreach (var goal in goals)
			{
				foreach (var step in goal.GoalSteps)
				{
					await eventRuntime.RunOnErrorStepEvents(new Error("Test error"), goal, step);
				}
				await pseudoRuntime.Received(goal.GoalSteps.Count).RunGoal(engine, Arg.Any<IPLangContextAccessor>(),
							goal.RelativeAppStartupFolderPath, "!Process", Arg.Any<Goal>());
			}
		}

		[TestMethod()]
		public void GetRuntimeEventsFilesTest_MakeSureRootEventsIsLast()
		{
			// 2 event files added
			string mainEventFile = Path.Join(fileSystem.BuildPath, "events", "Events", ISettings.GoalFileName);
			fileSystem.AddFile(mainEventFile, new MockFileData(""));
			var externalEventFile = Path.Join(fileSystem.BuildPath, "events", "external", "plang", "runtime", "Events", ISettings.GoalFileName);
			fileSystem.AddFile(externalEventFile, new MockFileData(""));
			//some other files
			fileSystem.AddFile(Path.Join(fileSystem.GoalsPath, "apps", "HelloWorld", ".build", "Process", ISettings.GoalFileName), new MockFileData(""));
			fileSystem.AddFile(Path.Join(fileSystem.GoalsPath, "HelloWorld", ISettings.GoalFileName), new MockFileData(""));

			var eventFiles = eventRuntime.GetEventsFiles(fileSystem.BuildPath, false);

			Assert.AreEqual(2, eventFiles.EventFiles.Count);
			Assert.AreEqual(externalEventFile, eventFiles.EventFiles[0]);
			Assert.AreEqual(mainEventFile, eventFiles.EventFiles[1]);

		}

		[TestMethod()]
		public void GetRuntimeEventsFilesTest_MakeSureRootBuilderEventsIsLast()
		{
			// 2 event files added
			string mainEventFile = Path.Join(fileSystem.BuildPath, "events", "BuilderEvents", ISettings.GoalFileName);
			fileSystem.AddFile(mainEventFile, new MockFileData(""));
			var externalEventFile = Path.Join(fileSystem.BuildPath, "events", "external", "plang", "runtime", "BuilderEvents", ISettings.GoalFileName);
			fileSystem.AddFile(externalEventFile, new MockFileData(""));
			//other files
			fileSystem.AddFile(Path.Join(fileSystem.BuildPath, "apps", "HelloWorld", ".build", "events", ISettings.GoalFileName), new MockFileData(""));
			fileSystem.AddFile(Path.Join(fileSystem.GoalsPath, "apps", "HelloWorld", ".build", "Process", ISettings.GoalFileName), new MockFileData(""));
			fileSystem.AddFile(Path.Join(fileSystem.GoalsPath, "helloWorld", ISettings.GoalFileName), new MockFileData(""));

			(var eventFiles, var error) = eventRuntime.GetEventsFiles(fileSystem.BuildPath, true);

			Assert.AreEqual(2, eventFiles.Count);
			Assert.AreEqual(externalEventFile, eventFiles[0]);
			Assert.AreEqual(mainEventFile, eventFiles[1]);

		}

		[TestMethod]
		public async Task LoadEvents()
		{

			var content = PrReaderHelper.GetPrFileRaw("Events/Events.pr");
			fileSystem.AddFile(Path.Join(fileSystem.BuildPath, "events", ISettings.GoalFileName), new MockFileData(content));
			prParser.ForceLoadAllGoals();

			eventRuntime.Load();
			var events = await eventRuntime.GetRuntimeEvents();

			Assert.AreEqual(3, events.Count);
			Assert.AreEqual(EventType.Before, events[0].EventType);
			Assert.AreEqual(EventScope.StartOfApp, events[0].EventScope);
			Assert.AreEqual("Process", events[0].GoalToCall.ToString());
			Assert.AreEqual(null, events[0].GoalToBindTo);

			Assert.AreEqual(EventType.After, events[1].EventType);
			Assert.AreEqual(EventScope.Step, events[1].EventScope);
			Assert.AreEqual("Process", events[1].GoalToCall.ToString());
			Assert.AreEqual("*", events[1].GoalToBindTo.ToString());
		}


		string eventJson = @"[
  {
    ""EventType"": ""After"",
    ""EventScope"": ""Step"",
    ""GoalToBindTo"": ""Start"",
    ""GoalToCall"": ""!SendEmail"",
    ""StepText"": """",
    ""IncludePrivate"": false
  },{
    ""EventType"": ""After"",
    ""EventScope"": ""Step"",
    ""GoalToBindTo"": ""TestApp.HelloWorld"",
    ""GoalToCall"": ""!SendEmail"",
    ""StepText"": ""write to db"",
    ""IncludePrivate"": false
  },
  {
    ""EventType"": ""Before"",
    ""EventScope"": ""Goal"",
    ""GoalToBindTo"": ""api/*"",
    ""GoalToCall"": ""!DoStuff"",
    ""IncludePrivate"": true
  },
  {
    ""EventType"": ""Before"",
    ""EventScope"": ""Step"",
    ""GoalToBindTo"": ""Start.goal"",
    ""GoalToCall"": ""!TestApp.HelloWorld""
  },
  {
    ""EventType"": ""Before"",
    ""EventScope"": ""StartOfApp"",
    ""GoalToBindTo"": ""Start:Process"",
    ""GoalToCall"": ""!BeforeStart"",
    ""IncludePrivate"": true
  },
  {
    ""EventType"":""Before"",
    ""EventScope"": ""EndOfApp"",
    ""GoalToBindTo"": ""*"",
    ""GoalToCall"": ""!SendEmail""
  }
]";

	}
}