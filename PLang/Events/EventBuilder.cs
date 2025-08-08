using LightInject;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using PLang.Building.Model;
using PLang.Building.Parsers;
using PLang.Errors;
using PLang.Errors.Builder;
using PLang.Errors.Events;
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

namespace PLang.Events
{

    public interface IEventBuilder
    {
        Task<(List<string>, IError?)> BuildEventsPr();
		(List<string>, IError?) GetEventGoalFiles();
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
		private readonly ITypeHelper typeHelper;

		public EventBuilder(ILogger logger, IPLangFileSystem fileSystem, ILlmServiceFactory llmServiceFactory,
            ISettings settings, IGoalParser goalParser, MemoryStack memoryStack, PrParser prParser, ITypeHelper typeHelper)
        {
            this.fileSystem = fileSystem;
            this.llmServiceFactory = llmServiceFactory;
            this.logger = logger;
            this.settings = settings;
            this.goalParser = goalParser;
            this.memoryStack = memoryStack;
            this.prParser = prParser;
			this.typeHelper = typeHelper;
		}

        public virtual async Task<(List<string>, IError?)> BuildEventsPr()
        {
            (var goalFiles, var error) = GetEventGoalFiles();
			
            if (error != null) return (goalFiles, error);
			if (goalFiles.Count == 0) return (new(), null);

			logger.LogInformation($"Building {goalFiles.Count} event file(s)");
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
                    if (StepHasBeenBuild(step, i, null))
                    {
                        logger.LogInformation($"- Event step {goal.GoalSteps[i].Text} already built");
                        continue;
                    }
                    logger.LogInformation($"- Building event step {goal.GoalSteps[i].Text}");
                    var promptMessage = new List<LlmMessage>();
                    var eventTypeScheme = TypeHelper.GetJsonSchema(typeof(EventType));
                    var eventScope = TypeHelper.GetJsonSchema(typeof(EventScope));

                    promptMessage.Add(new LlmMessage("system", GetSystemPrompt(filePath.EndsWith("BuildEvents.goal"))));
                 
                    promptMessage.Add(new LlmMessage("user", step.Text));

                    var llmRequest = new LlmRequest("Events", promptMessage);
                    (var eventBinding, var queryError) = await llmServiceFactory.CreateHandler().Query<EventBinding>(llmRequest);
                    if (queryError != null) return ([], queryError);
                    if (eventBinding == null)
                    {
                       return ([], new BuilderEventError($"Could not build an events from step {step.Text} in {filePath}. LLM didn't give any response. Try to rewriting the event.", eventBinding, Step: step, Goal: step.Goal));
                    }


					(var foundGoal, error) = GoalHelper.GetGoal(step.RelativeGoalPath, step.Goal.AbsoluteAppStartupFolderPath, eventBinding.GoalToCall, goalParser.GetGoals(), new());
					if (error != null) return (new(), new BuilderError(error));

					eventBinding.GoalToCall.Path = foundGoal.RelativePrPath;

					if (eventBinding.EventScope == EventScope.Module)
					{
						/*
						var modules = typeHelper.GetModulesAsString();
						promptMessage = new();
						promptMessage.Add(new LlmMessage("system", $@"Which module from <modules> does the user want to bind to?\n<modules>\n{modules}\n</modules>"));
						promptMessage.Add(new LlmMessage("user", step.Text));
						llmRequest = new LlmRequest("ModuleEvents", promptMessage);
						llmRequest.scheme = "{module:string}";
						(var module, queryError) = await llmServiceFactory.CreateHandler().Query<string>(llmRequest);

						var type = typeHelper.GetRuntimeType(module);
						var methods = typeHelper.GetMethodNamesAsString(type);


						promptMessage = new();
						promptMessage.Add(new LlmMessage("system", $@"Which <methods> does the user want to bind to?\n<methods>\n{methods}\n</methods>"));
						promptMessage.Add(new LlmMessage("user", step.Text));
						llmRequest = new LlmRequest("ModuleEvents", promptMessage);
						llmRequest.scheme = "{methods:string[]}";
						(var methodsToBind, queryError) = await llmServiceFactory.CreateHandler().Query<string[]>(llmRequest);
						
						int s = 0;*/

					}


                    if (eventBinding.GoalToCall == eventBinding.GoalToBindTo)
                    {
                        logger.LogError($"{step.Text} binds an event to same goal it is calling. This is not allowed as it will cause an infiniate loop. Event is ignored.");
                        continue;
                    }
                    step.AppStartupPath = goal.AbsoluteAppStartupFolderPath;
                    step.PrFileName = "Events.pr";
                    step.AbsolutePrFilePath = Path.Join(goal.AbsolutePrFolderPath, step.PrFileName);
                    step.RelativePrPath = Path.Join(goal.RelativePrFolderPath, step.PrFileName);
                    step.Generated = DateTime.Now;

                    if (eventBinding != null)
                    {
                        error = validateEventModel(eventBinding, step);
                        if (error != null) return (new(), error);

                        step.EventBinding = eventBinding;
                        step.IsEvent = true;
					}
                }

                if (!fileSystem.Directory.Exists(goal.AbsolutePrFolderPath))
                {
                    fileSystem.Directory.CreateDirectory(goal.AbsolutePrFolderPath);
                }
                validGoalFiles.Add(goal.AbsoluteGoalPath);
                fileSystem.File.WriteAllText(goal.AbsolutePrFilePath, JsonConvert.SerializeObject(goal, Formatting.Indented));
            }
            return (validGoalFiles, null);
        }

		public static string GetSystemPrompt(bool isBuildEvents)
		{
			string buildLlmSystemInfo = "";
			if (isBuildEvents)
			{
				buildLlmSystemInfo = @$"This event are for the builder. ";

			}
			string system = $@"
User will provide event binding, you will be provided with c# model to map the code to. 
{buildLlmSystemInfo}

EventType is required, Error defaults to 'After' EventType if not defined by user.
EventScope defines at what stage the event should run, it can be on goal, step, start of app, end of app, etc. See EventScope definition below.
GoalToBindTo is required. This can a specific Goal or more generic, such as bind to all goals in specific folder. When undefined set as *. Any pattern from user must be translated to regex version without ^ and $, e.g. user defines folder matching pattern such as /ui/*, it should then be the regex version of that: /ui/.*
GoalToCall is required. This should be a specific goal, keep path as user defines  
GoalToCallParameters parameters that user wants to sent to goal
StepNumber & StepText reference a specific step that the user wants to bind to
IncludePrivate defines if user wants to include private goals, he needs to specify this specifically to be true
WaitForExecution: indicates if goal should by run and forget
RunOnlyOnStartParameter: parameters at the startup of the application, it should start with --, for example --debug
ErrorKey: is key of the error message (case insensitive)
ErrorMessage: checks if error.Message contains a message  (case insensitive)
ErrorStatusCode: matches the status code of the error message
Map correct number to EventType and EventScope
Include OS goals must be defined by user when set to true

EventType {{ Before , After }}
EventScope {{ StartOfApp, EndOfApp, AppError, RunningApp, Goal, Step, Module, GoalError, StepError, ModuleError }}


<examples>
- before goal end ... => EventType = After, EventScope = Goal  //indicates that this should run just before it end or after it has runned
<examples>
";
			return system;
		}

		private IError? validateEventModel(EventBinding eventBinding, GoalStep step)
		{
            if (string.IsNullOrEmpty(eventBinding.EventScope))
            {
                return new BuilderEventError($@"You must define the where the event should run. Is it step, goal, start of app, end of app, etc.?", eventBinding, Key: "EventBuilder",
					Step: step, Goal: step.Goal,
					FixSuggestion: "- before each step, call goal !DoStuff\n- before any goal, call !BeforeGoal",
                    HelpfulLinks: "https://github.com/PLangHQ/plang/blob/main/Documentation/Events.md"
					);
            }
			if (eventBinding.EventScope == "StartOfApp" || eventBinding.EventScope == "EndOfApp"
				 || eventBinding.EventScope == "AppError" || eventBinding.EventScope == "RunningApp")
            {
                eventBinding = new EventBinding(eventBinding.EventType, eventBinding.EventScope, null, eventBinding.GoalToCall, eventBinding.IncludePrivate, eventBinding.StepNumber, eventBinding.StepText, eventBinding.WaitForExecution, eventBinding.RunOnlyOnStartParameter, eventBinding.OnErrorContinueNextStep);

            }
            return null;
		}

		private bool StepHasBeenBuild(GoalStep step, int stepIndex, List<string>? excludeModules)
        {
            if (step.Goal.AbsolutePrFilePath == null) return false;

            var prGoal = prParser.ParsePrFile(step.Goal.AbsolutePrFilePath);

            if (prGoal == null || prGoal.GoalSteps == null || prGoal.GoalSteps.Count <= stepIndex) return false;
            if (stepIndex == prGoal.GoalSteps.Count || prGoal.GoalSteps[stepIndex].EventBinding == null) return false;

            var isFound = prGoal.GoalSteps.FirstOrDefault(p => p.PrFileName != null && p.Text == step.Text && p.Number == stepIndex) != null;

            return isFound;

        }


        public (List<string>, IError?) GetEventGoalFiles()
        {
            var eventsPath = Path.Join(fileSystem.GoalsPath, "events");
            if (fileSystem.File.Exists(eventsPath + ".goal"))
            {
                return (new(), new Error("Events.goal file must be located in the 'events' folder."));
            }
            if (fileSystem.File.Exists(eventsPath + "build.goal"))
            {
				return (new(), new Error("EventsBuild.goal file must be located in the 'events' folder."));
            }

			List<string> files = new();
			
			if (fileSystem.Directory.Exists(eventsPath))
			{
				files = fileSystem.Directory.GetFiles(eventsPath, "*.goal", SearchOption.AllDirectories)
				.Where(file =>
				{
					var isMatch = Regex.IsMatch(Path.GetFileName(file).ToLower(), @"(events|eventsbuild)\.goal$");
					return isMatch;
				}).ToList();
			}


			return (files, null);
        }
    }

}
