using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using PLang.Building.Model;
using PLang.Building.Parsers;
using PLang.Exceptions;
using PLang.Interfaces;
using PLang.Models;
using PLang.Runtime;
using PLang.Services.LlmService;
using PLang.Services.SettingsService;
using PLang.Utils;
using System.Runtime.ConstrainedExecution;
using System.Text.RegularExpressions;
using static PLang.Modules.BaseBuilder;

namespace PLang.Building.Events
{

	public interface IEventBuilder
	{
		Task<List<string>> BuildEventsPr();
		List<string> GetEventGoalFiles();
	}
	public class EventBuilder : IEventBuilder
	{
		private readonly IPLangFileSystem fileSystem;
		private readonly ILlmServiceFactory llmServiceFactory;
		private readonly ILogger logger;
		private readonly ISettings settings;
		private readonly IGoalParser goalParser;
		private readonly MemoryStack memoryStack;
		private readonly PrParser prParser;

		public EventBuilder(ILogger logger, IPLangFileSystem fileSystem, ILlmServiceFactory llmServiceFactory,
			ISettings settings, IGoalParser goalParser, MemoryStack memoryStack, PrParser prParser)
		{
			this.fileSystem = fileSystem;
			this.llmServiceFactory = llmServiceFactory;
			this.logger = logger;
			this.settings = settings;
			this.goalParser = goalParser;
			this.memoryStack = memoryStack;
			this.prParser = prParser;
		}

		public virtual async Task<List<string>> BuildEventsPr()
		{
			var goalFiles = GetEventGoalFiles();
			logger.LogDebug($"Building {goalFiles.Count} event file(s)");
			var validGoalFiles = new List<string>();
			foreach (var filePath in goalFiles)
			{
				var goals = goalParser.ParseGoalFile(filePath);
				var goal = goals.FirstOrDefault();

				if (goal == null)
				{
					logger.LogWarning($"No Events goal found for {filePath}");
					continue;
				}
				if (goal.GoalSteps.Count == 0)
				{
					logger.LogWarning($"No steps found in {goal.GoalName} in file {filePath}");
					continue;
				}

				for (int i = 0; i < goal.GoalSteps.Count; i++)
				{
					var step = goal.GoalSteps[i];
					if (StepHasBeenBuild(step, i, null)) continue;

					var promptMessage = new List<LlmMessage>();
					promptMessage.Add(new LlmMessage("system", $@"
User will provide event binding, you will be provided with c# model to map the code to. 

EventType is required
EventScope is required
GoalToBindTo is required. This can a specific Goal or more generic, such as bind to all goals in specific folder.
GoalToCall is required. This should be a specific goal, should start with !. Example: !AppName/GoalName.  
StepNumber & StepText reference a specific step that the user wants to bind to
IncludePrivate defines if user wants to include private goals, he needs to specify this specifically to be true
"));
					promptMessage.Add(new LlmMessage("assistant", $@"	
Map correct number to EventType and EventScope

enum EventType {{ Before = 0, After = 1, OnError = 2 }}
enum EventScope {{ StartOfApp = 0, EndOfApp = 1,RunningApp = 2,	Goal = 20, Step = 30 }}"));
					promptMessage.Add(new LlmMessage("user", step.Text));

					var llmRequest = new LlmRequest("Events", promptMessage);
					var eventModel = await llmServiceFactory.CreateHandler().Query<EventBinding>(llmRequest);
					if (eventModel == null)
					{
						throw new BuilderStepException($"Could not build an events from step {step.Text} in {filePath}. LLM didn't give any response. Try to rewriting the event.", step);
					}


					if (eventModel.GoalToCall == eventModel.GoalToBindTo)
					{
						logger.LogError($"{step.Text} binds an event to same goal it is calling. This is not allowed as it will cause an infiniate loop. Event is ignored.");
						continue;
					}
					step.AppStartupPath = goal.AbsoluteAppStartupFolderPath;
					step.PrFileName = "Events.pr";
					step.AbsolutePrFilePath = Path.Join(goal.AbsolutePrFolderPath, step.PrFileName);
					step.RelativePrPath = Path.Join(goal.RelativePrFolderPath, step.PrFileName);
					step.Generated = DateTime.Now;
					step.Custom.Add("Event", eventModel);
				}

				if (!fileSystem.Directory.Exists(goal.AbsolutePrFolderPath))
				{
					fileSystem.Directory.CreateDirectory(goal.AbsolutePrFolderPath);
				}
				validGoalFiles.Add(goal.AbsoluteGoalPath);
				fileSystem.File.WriteAllText(goal.AbsolutePrFilePath, JsonConvert.SerializeObject(goal, Formatting.Indented));
			}
			return validGoalFiles;
		}


		private bool StepHasBeenBuild(GoalStep step, int stepIndex, List<string>? excludeModules)
		{
			if (step.Goal.AbsolutePrFilePath == null) return false;

			var prGoal = prParser.ParsePrFile(step.Goal.AbsolutePrFilePath);
			
			if (prGoal == null || prGoal.GoalSteps == null) return false;
			if (stepIndex == prGoal.GoalSteps.Count || prGoal.GoalSteps[stepIndex].Custom == null) return false;

			if (!prGoal.GoalSteps[stepIndex].Custom.ContainsKey("Event") || prGoal.GoalSteps[stepIndex].Custom["Event"] == null) return false;


			var isFound = prGoal.GoalSteps.FirstOrDefault(p => p.Text == step.Text && p.Number == stepIndex) != null;


			return isFound;

		}


		public List<string> GetEventGoalFiles()
		{
			var eventsPath = Path.Join(fileSystem.GoalsPath, "events");
			if (fileSystem.File.Exists(eventsPath + ".goal"))
			{
				throw new BuilderException("Events.goal file must be located in the events folder.");
			}
			if (fileSystem.File.Exists(eventsPath + "build.goal"))
			{
				throw new BuilderException("EventsBuild.goal file must be located in the events folder.");
			}

			if (!fileSystem.Directory.Exists(eventsPath)) return new();

			return fileSystem.Directory.GetFiles(eventsPath, "*.goal", SearchOption.AllDirectories)
				.Where(f => Regex.IsMatch(Path.GetFileName(f).ToLower(), @"(events|eventsbuild)\.goal$"))
					 .ToList();
		}
	}

}
