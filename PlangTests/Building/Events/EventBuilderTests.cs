using LightInject;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json;
using NSubstitute;
using PLang.Building.Model;
using PLang.Building.Parsers;
using PLang.Errors;
using PLang.Events;
using PLang.Exceptions;
using PLang.Interfaces;
using PLang.Models;
using PLangTests;
using PLangTests.Mocks;
using System.IO.Abstractions.TestingHelpers;

namespace PLang.Building.Events.Tests
{
    [TestClass()]
	public class EventBuilderTests : BasePLangTest
	{
		[TestInitialize]
		public void Init()
		{
			base.Initialize();
		}

		[TestMethod()]
		public void GetEventGoalFilesTest()
		{
			
			var settings = container.GetInstance<ISettings>();
			var fileSystem = (PLangMockFileSystem)container.GetInstance<IPLangFileSystem>();

			string content = @"Events";

			
			fileSystem.AddFile(Path.Join(fileSystem.GoalsPath, "events", "Events.goal"), new MockFileData(content));
			fileSystem.AddFile(Path.Join(fileSystem.GoalsPath, "events", "BuilderEvents.goal"), new MockFileData(content));
			fileSystem.AddFile(Path.Join(fileSystem.GoalsPath, "apps", "HelloWorld", "events", "Events.goal"), new MockFileData(content));

			var eventBuilder = container.GetInstance<EventBuilder>();
			var files = eventBuilder.GetEventGoalFiles();
			Assert.IsNotNull(files);
			Assert.AreEqual(2, files.Item1.Count);
			Assert.AreEqual(files.Item1[0], Path.Join(fileSystem.GoalsPath, "events", "Events.goal"));

			//var result = eventBuilder.BuildEventsPr().Wait();
		
		}

		[TestMethod()]
		public void BuildEventsPr_No_Steps_Found_Test()
		{

			var eventBuilder = container.GetInstance<EventBuilder>();
		;
			var settings = container.GetInstance<ISettings>();
			var fileSystem = (PLangMockFileSystem)container.GetInstance<IPLangFileSystem>();

			string content = @"Events";

			fileSystem.AddFile(Path.Join(fileSystem.GoalsPath, "apps", "SomeApp", "events", "Events.goal"), new MockFileData(content));
			fileSystem.AddFile(Path.Join(fileSystem.GoalsPath, "events", "Events.goal"), new MockFileData(content));
			fileSystem.AddFile(Path.Join(fileSystem.GoalsPath, "apps", "HelloWorld", "events", "Events.goal"), new MockFileData(content));

			eventBuilder.BuildEventsPr().Wait();

			logger.Received().Log(Microsoft.Extensions.Logging.LogLevel.Warning, Arg.Is<string>(s => s.Contains("No steps found")));
			
		}

		[TestMethod()]
		public void BuildEventsPr_No_Events_Found_Test()
		{

			var eventBuilder = container.GetInstance<EventBuilder>();
			
			var settings = container.GetInstance<ISettings>();
			var fileSystem = (PLangMockFileSystem)container.GetInstance<IPLangFileSystem>();

			string content = @"";

			fileSystem.AddFile(Path.Join(fileSystem.GoalsPath, "events", "Events.goal"), new MockFileData(content));

			eventBuilder.BuildEventsPr().Wait();

			logger.Received().Log(Microsoft.Extensions.Logging.LogLevel.Warning, Arg.Is<string>(s => s.Contains("No Events goal")));

		}

		[TestMethod()]
		public async Task BuildEventsPr_Goals_Test_ShouldThrowBuilderStepException()
		{
			var settings = container.GetInstance<ISettings>();
			var fileSystem = (PLangMockFileSystem)container.GetInstance<IPLangFileSystem>();

			string content = @"Events
- before each goal, call DoStuff
- after each step call DontDoStuff";

			fileSystem.AddFile(Path.Join(fileSystem.GoalsPath, "events", "Events.goal"), new MockFileData(content));

			var prParser = Substitute.For<PrParser>(fileSystem);
			prParser.ParsePrFile(Arg.Any<string>()).Returns(new Goal());

			var eventBuilder = container.GetInstance<EventBuilder>();
			(var result, var error) = await eventBuilder.BuildEventsPr();

			Assert.IsNotNull(error);
			Assert.AreEqual(0, result.Count);
			//Assert.ThrowsAsync<BuilderStepException>(() => eventBuilder.BuildEventsPr());
		}

		private bool ContainsStep(LlmRequest llmRequest, string step)
		{
			if (llmRequest == null) return false;

			foreach (var message in llmRequest.promptMessage)
			{
				foreach (var content in message.Content)
				{
					if (content.Text.Contains(step)) return true;
				}
			}
			return false;
		}


		[TestMethod()]
		public async Task BuildEventsPr_Goals_WithResults_Test()
		{
			var settings = container.GetInstance<ISettings>();

			string content = @"Events
- before each goal in api/* call !DoStuff
- before each step call !Debugger.SendInfo
- after Run.goal, call !AfterRun
- after step nr 1 in Startup.goal, run ProcessImage";

			fileSystem.AddFile(Path.Join(fileSystem.GoalsPath, "events", "Events.goal"), new MockFileData(content));

			var result0 = Task.FromResult((JsonConvert.DeserializeObject<EventBinding>(aiResponses[0]), default(IError)));
			var result1 = Task.FromResult((JsonConvert.DeserializeObject<EventBinding>(aiResponses[1]), default(IError)));
			var result2 = Task.FromResult((JsonConvert.DeserializeObject<EventBinding>(aiResponses[2]), default(IError)));
			var result3 = Task.FromResult((JsonConvert.DeserializeObject<EventBinding>(aiResponses[3]), default(IError)));

			var aiService = container.GetInstance<ILlmService>();
			aiService.Query<EventBinding>(Arg.Is<LlmRequest>(p => ContainsStep(p, "before each goa")))
				.Returns(result0);
			aiService.Query<EventBinding>(Arg.Is<LlmRequest>(p => ContainsStep(p, "before each step")))
				.Returns(result1);
			aiService.Query<EventBinding>(Arg.Is<LlmRequest>(p => ContainsStep(p, "after Run.goal")))
				.Returns(result2);
			aiService.Query<EventBinding>(Arg.Is<LlmRequest>(p => ContainsStep(p, "after step nr 1")))
				.Returns(result3);

			var eventBuilder = container.GetInstance<EventBuilder>();
			await eventBuilder.BuildEventsPr();

			var buildPathFolder = Path.Join(fileSystem.BuildPath, "events");
			var eventFile = fileSystem.File.ReadAllText(Path.Combine(buildPathFolder, "events", ISettings.GoalFileName));

			var goal = JsonConvert.DeserializeObject<Goal>(eventFile);

			foreach (var step in goal.GoalSteps)
			{
				Assert.IsTrue(content.Contains(step.Text));
			}
			
			Assert.AreEqual(4, goal.GoalSteps.Count);

			var eve = JsonConvert.DeserializeObject<EventBinding>(goal.GoalSteps[0].Custom["Event"].ToString());
			Assert.AreEqual(EventType.Before, eve.EventType);
			Assert.AreEqual(EventScope.Goal, eve.EventScope);

		}


		string[] aiResponses = { @"
			  {
				""EventType"": ""Before"",
				""EventScope"": ""Goal"",
				""GoalToBindTo"": ""api/*"",
				""GoalToCall"": ""!DoStuff""
			  }", @"
			  {
				""EventType"": ""Before"",
				""EventScope"": ""Step"",
				""GoalToBindTo"": ""*"",
				""GoalToCall"": ""!Debugger.SendInfo""
			  }", @"
			  {
				""EventType"": ""After"",
				""EventScope"": ""Goal"",
				""GoalToBindTo"": ""Run.goal"",
				""GoalToCall"": ""!AfterRun""
			  }", @"
			  {
				""EventType"": ""After"",
				""EventScope"": ""Step"",
				""GoalToBindTo"": ""Startup.goal"",
				""GoalToCall"": ""ProcessImage"",
				""StepNumber"": 1
			  }" };
	}
}