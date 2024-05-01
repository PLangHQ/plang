using LightInject;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using PLang.Building.Model;
using PLang.Building.Parsers;
using PLang.Container;
using PLang.Errors;
using PLang.Errors.Handlers;
using PLang.Exceptions;
using PLang.Interfaces;
using PLang.Runtime;
using PLang.Utils;
using System.Text.RegularExpressions;
using Websocket.Client.Logging;

namespace PLang.Events
{

	public interface IEventRuntime
	{
		Task<List<EventBinding>> GetBuilderEvents();
		Task<List<EventBinding>> GetRuntimeEvents();
		List<string> GetRuntimeEventsFiles(string buildPath, string eventFolder);
		bool GoalHasBinding(Goal goal, EventBinding eventBinding);
		bool IsStepMatch(GoalStep step, EventBinding eventBinding);
		Task Load(IServiceContainer container, bool builder = false);
		Task RunBuildGoalEvents(string eventType, Goal goal);
		Task RunBuildStepEvents(string eventType, Goal goal, GoalStep step, int stepIdx);
		Task<IError?> RunGoalEvents(PLangAppContext context, string eventType, Goal goal);
		Task<IError?> RunStartEndEvents(PLangAppContext context, string eventType, string eventScope, bool isBuilder = false);
		Task<IError?> RunStepEvents(PLangAppContext context, string eventType, Goal goal, GoalStep step);
		Task<(bool, IError?)> RunOnErrorStepEvents(PLangAppContext context, IError error, Goal goal, GoalStep step, ErrorHandler? stepErrorHandler = null);
		Task<IError?> RunGoalErrorEvents(PLangAppContext context, Goal goal, int goalStepIndex, IError error);
		Task<IError?> AppErrorEvents(PLangAppContext context, IError error);
	}
	public class EventRuntime : IEventRuntime
	{
		private readonly IPLangFileSystem fileSystem;
		private readonly ISettings settings;
		private readonly IPseudoRuntime pseudoRuntime;
		private readonly PrParser prParser;
		private readonly IEngine engine;
		private readonly IErrorHandlerFactory errorHandlerFactory;
		private readonly ILogger logger;
		private static List<EventBinding>? events = null;


		public EventRuntime(IPLangFileSystem fileSystem, ISettings settings, IPseudoRuntime pseudoRuntime,
			PrParser prParser, IEngine engine, IErrorHandlerFactory exceptionHandlerFactory, ILogger logger)
		{
			this.fileSystem = fileSystem;
			this.settings = settings;
			this.pseudoRuntime = pseudoRuntime;
			this.prParser = prParser;
			this.engine = engine;
			this.errorHandlerFactory = exceptionHandlerFactory;
			this.logger = logger;
		}

		public async Task<List<EventBinding>> GetBuilderEvents()
		{
			if (events == null)
			{
				throw new BuilderException("Events are null. GetBuilderEvents() cannot be called before Load");
			}
			return events!;
		}

		public async Task<List<EventBinding>> GetRuntimeEvents()
		{
			if (events == null)
			{
				throw new RuntimeException("Events are null. GetRuntimeEvents() cannot be called before Load");
			}
			return events!;
		}
		public async Task Load(IServiceContainer container, bool builder = false)
		{
			events = new List<EventBinding>();

			string eventsFolder = builder ? Path.Combine("events", "BuildEvents") : "events";
			var eventsFiles = GetRuntimeEventsFiles(fileSystem.BuildPath, eventsFolder);

			foreach (var eventFile in eventsFiles)
			{
				var goal = prParser.GetGoal(eventFile);
				if (goal == null)
				{
					continue;
				}
				foreach (var step in goal.GoalSteps)
				{
					if (!step.Custom.ContainsKey("Event") || step.Custom["Event"] == null) continue;
					var eve = JsonConvert.DeserializeObject<EventBinding>(step.Custom["Event"].ToString()!);
					if (eve == null) continue;

					AppContext.TryGetSwitch(ReservedKeywords.Debug, out bool isDebugMode);

					// dont run event that is meant for debug mode
					if (eve.RunOnlyInDebugMode && !isDebugMode)
					{
						continue;
					}
					events.Add(eve);
				}

				if (goal.Injections != null)
				{
					foreach (var injection in goal.Injections)
					{
						container.RegisterForPLangUserInjections(injection.Type, injection.Path, injection.IsGlobal, injection.EnvironmentVariable, injection.EnvironmentVariableValue);
					}
				}
			}
			logger.LogDebug("Loaded {0} events", events.Count);

			prParser.ForceLoadAllGoals();
		}

		public List<string> GetRuntimeEventsFiles(string buildPath, string eventFolder)
		{
			if (!fileSystem.Directory.Exists(buildPath))
			{
				throw new RuntimeException(".build folder does not exists. Run 'plang build' first.");
			}
			var eventsFolder = Path.Join(buildPath, eventFolder);
			if (!fileSystem.Directory.Exists(eventsFolder)) return new();

			var eventFiles = fileSystem.Directory.GetFiles(eventsFolder, ISettings.GoalFileName, SearchOption.AllDirectories)
					.ToList();

			if (eventFiles.Count == 1) return eventFiles;

			var rootEvent = eventFiles.FirstOrDefault(p => p == Path.Join(buildPath, eventFolder, "Events", ISettings.GoalFileName));
			if (rootEvent != null)
			{
				eventFiles.Remove(rootEvent);
				eventFiles.Add(rootEvent);
			}
			return eventFiles;

		}

		public async Task<IError?> RunStartEndEvents(PLangAppContext context, string eventType, string eventScope, bool isBuilder = false)
		{
			events = (isBuilder) ? await GetBuilderEvents() : await GetRuntimeEvents();

			if (events == null || context.ContainsKey(ReservedKeywords.IsEvent)) return null;

			List<EventBinding> eventsToRun = events.Where(p => p.EventScope == eventScope).ToList();

			for (var i = 0; i < eventsToRun.Count; i++)
			{
				var eve = eventsToRun[i];
				var parameters = new Dictionary<string, object?>();
				parameters.Add(ReservedKeywords.Event, eve);
				context.Add(ReservedKeywords.IsEvent, true);

				logger.LogDebug("Run event type {0} on scope {1}, binding to {2} calling {3}", eventType, eventScope, eve.GoalToBindTo, eve.GoalToCall);
				var task = pseudoRuntime.RunGoal(engine, context, Path.DirectorySeparatorChar.ToString(), eve.GoalToCall, parameters);
				if (eve.WaitForExecution)
				{
					await task;
				}
				(_, var error) = task.Result;

				context.Remove(ReservedKeywords.IsEvent);
				if (context.TryGetValue(ReservedKeywords.MemoryStack, out var obj0) && obj0 != null)
				{
					var memoryStack = (MemoryStack)obj0;
					memoryStack.Remove(ReservedKeywords.Event);
				}

				return error;
			}
			return null;

		}


		public async Task<IError?> AppErrorEvents(PLangAppContext context, IError error)
		{
			var eventsToRun = events.Where(p => p.EventScope == EventScope.AppError).ToList();
			if (eventsToRun.Count > 0)
			{
				foreach (var eve in events)
				{
					var runError = await Run(context, eve, null, null, error);
					if (runError != null) return runError;
				}
			}
			else
			{
				await ShowDefaultError(error, null);
			}
			return null;

		}

		private async Task<IError?> HandleError(PLangAppContext context, Goal goal, IError error, GoalStep? step, List<EventBinding> eventsToRun)
		{
			if (eventsToRun.Count == 0) return error;

			for (var i = 0; i < eventsToRun.Count; i++)
			{
				var eve = eventsToRun[i];
				if (!GoalHasBinding(goal, eve)) continue;

				var errorRun = await Run(context, eve, goal, step, error);
				if (errorRun != null) return errorRun;
			}
			return new ErrorHandled(error);
		}

		public async Task RunBuildGoalEvents(string eventType, Goal goal)
		{

			var events = await GetBuilderEvents();
			if (events.Count == 0) return;

			PLangAppContext context = new();
			context.Add(ReservedKeywords.Goal, goal);
			await RunGoalEvents(context, eventType, goal);

		}

		public async Task<IError?> RunGoalEvents(PLangAppContext context, string eventType, Goal goal)
		{
			if (events == null || context.ContainsKey(ReservedKeywords.IsEvent)) return null;
			var eventsToRun = events.Where(p => p.EventType == eventType && p.EventScope == EventScope.Goal).ToList();
			for (var i = 0; i < eventsToRun.Count; i++)
			{
				var eve = eventsToRun[i];
				if (!GoalHasBinding(goal, eve)) continue;

				var errors = await Run(context, eve, goal);
				if (errors != null) return errors;
			}
			return null;
		}

		public async Task<IError?> RunGoalErrorEvents(PLangAppContext context, Goal goal, int goalStepIndex, IError error)
		{
			if (events == null || context.ContainsKey(ReservedKeywords.IsEvent)) return null;

			var step = (goalStepIndex != -1 && goalStepIndex < goal.GoalSteps.Count) ? goal.GoalSteps[goalStepIndex] : null;
			var eventsToRun = events.Where(p => p.EventScope == EventScope.GoalError).ToList();

			return await HandleError(context, goal, error, step, eventsToRun);

		}

		private async Task ShowDefaultError(IError error, GoalStep? step)
		{
			await errorHandlerFactory.CreateHandler().ShowError(error, 500, "error", error.Message, step);
		}

		private async Task<IError?> Run(PLangAppContext context, EventBinding eve, Goal? goal, GoalStep? step = null, IError? error = null)
		{
			try
			{
				var parameters = new Dictionary<string, object?>();
				parameters.Add(ReservedKeywords.Event, eve);
				parameters.Add(ReservedKeywords.Goal, goal);
				parameters.Add("error", error);
				context.TryAdd(ReservedKeywords.IsEvent, true);
				if (step != null) parameters.Add(ReservedKeywords.Step, step);
				string relativeAppStartupFolderPath = goal != null ? goal.RelativeAppStartupFolderPath : fileSystem.RelativeAppPath;

				logger.LogDebug("Run event type {0} on scope {1}, binding to {2} calling {3}", eve.EventType.ToString(), eve.EventScope.ToString(), eve.GoalToBindTo, eve.GoalToCall);

				var task = pseudoRuntime.RunGoal(engine, context, relativeAppStartupFolderPath, eve.GoalToCall, parameters, goal);
				if (eve.WaitForExecution)
				{
					await task;
				}

				if (task.Exception != null)
				{
					var exception = task.Exception.InnerException ?? task.Exception;
					return new EventError(exception.Message, eve, goal, step, "EventError", exception);
				}

				return task.Result.error;
			}
			finally
			{
				context.Remove(ReservedKeywords.IsEvent);
				if (context.TryGetValue(ReservedKeywords.MemoryStack, out var obj0) && obj0 != null)
				{
					var memoryStack = (MemoryStack)obj0;
					memoryStack.Remove(ReservedKeywords.Event);
					memoryStack.Remove(ReservedKeywords.Goal);
					memoryStack.Remove(ReservedKeywords.Step);
				}
			}


		}
		public async Task RunBuildStepEvents(string eventType, Goal goal, GoalStep step, int stepIdx)
		{
			var context = new PLangAppContext();
			context.Add(ReservedKeywords.Goal, goal);
			context.Add(ReservedKeywords.Step, step);
			context.Add(ReservedKeywords.StepIndex, stepIdx);

			await RunStepEvents(context, eventType, goal, step);
		}


		public async Task<IError?> RunStepEvents(PLangAppContext context, string eventType, Goal goal, GoalStep step)
		{
			if (events == null || context.ContainsKey(ReservedKeywords.IsEvent)) return null;
			var eventsToRun = events.Where(p => p.EventType == eventType && p.EventScope == EventScope.Step).ToList();
			for (var i = 0; i < eventsToRun.Count; i++)
			{
				var eve = eventsToRun[i];
				if (GoalHasBinding(goal, eve) && IsStepMatch(step, eve))
				{
					return await Run(context, eve, goal, step);
				}
			}
			return null;
		}
		public async Task<(bool, IError?)> RunOnErrorStepEvents(PLangAppContext context, IError error, Goal goal, GoalStep step, ErrorHandler? stepErrorHandler = null)
		{
			if (events == null || context.ContainsKey(ReservedKeywords.IsEvent)) return (false, error);
			if (error is EndGoal)
			{
				return (false, error);
			}

			List<EventBinding> eventsToRun = new();
			eventsToRun.AddRange(events.Where(p => p.EventType == EventType.Before && p.EventScope == EventScope.StepError).ToList());

			bool shouldContinueNextStep = false;
			if (stepErrorHandler != null)
			{
				var goalToCall = GetErrorHandlerStep(step, error);
				if (goalToCall != null)
				{
					shouldContinueNextStep = stepErrorHandler.ContinueToNextStep;
					var eventBinding = new EventBinding(EventType.Before, EventScope.StepError, goal.RelativeGoalPath, goalToCall, true, step.Number, step.Text, true, false, false);
					eventsToRun.Add(eventBinding);
				}
			}

			eventsToRun.AddRange(events.Where(p => p.EventType == EventType.After && p.EventScope == EventScope.StepError).ToList());

			if (eventsToRun.Count == 0)
			{
				if (goal.ParentGoal != null) return (false, error);
				
				await ShowDefaultError(error, step);
			}
			else
			{
				foreach (var eve in eventsToRun)
				{
					if (GoalHasBinding(goal, eve) && IsStepMatch(step, eve))
					{
						await Run(context, eve, goal, step, error);
					}

					//comment: this might be a bad idea, what happens when you have multiple events, on should continue other not
					if (!shouldContinueNextStep) shouldContinueNextStep = eve.OnErrorContinueNextStep;
				}
			}
			return (shouldContinueNextStep, new ErrorHandled(error));
		}

		private string? GetErrorHandlerStep(GoalStep step, IError error)
		{
			if (step.ErrorHandler == null) return null;
			var except = step.ErrorHandler.OnExceptionContainingTextCallGoal;

			if (except == null) return null;
			if (step.ErrorHandler.IgnoreErrors && except.Count == 0) return null;

			foreach (var errorHandler in except)
			{
				if (errorHandler.Key == "*" || error.ToString().ToLower().Contains(error.Key.ToLower()))
				{
					return errorHandler.Value;
				}
			}
			return null;
		}


		/*
		 * 
		 */
		public bool IsStepMatch(GoalStep step, EventBinding eventBinding)
		{
			if (eventBinding.StepNumber == null && eventBinding.StepText == null) return true;
			if (eventBinding.StepNumber != null && step.Number == eventBinding.StepNumber)
			{
				return true;
			}

			if (eventBinding.StepText != null && step.Text.ToLower().Contains(eventBinding.StepText.ToLower()))
			{
				return true;
			}
			return false;

		}


		/*
		 * Maybe to liberal at adding events to method. With great power comes great responsibility.
		 * 
		 * To prevent accidental overloading, event binding defaults to only bind to public goals.
		 * 
		 * GoalToBindTo = Hello => Binds to any goal name called Hello in current app, if multiple then it will bind to all
		 * GoalToBindTo = Hello.goal => Binds to goal in files Hello.goal in current app, user can define to bind to private goals, if multiple then it will bind to all
		 * GoalToBindTo = api/* => Binds to any goal with in api folder, if multiple then it will bind to all
		 * GoalToBindTo = SampleApp.Hello => Binds to any goal name called Hello in /apps/SampleApp, if multiple then it will bind to all
		 * GoalToBindTo = GenerateData(.goal)?:ProcessFile => Binds to any goal name called ProcessFile in the GenerateData.goal, if multiple then it will bind to all
		 */
		public bool GoalHasBinding(Goal goal, EventBinding eventBinding)
		{
			if (goal.Visibility == Visibility.Private && !eventBinding.IncludePrivate || eventBinding.GoalToBindTo == null) return false;

			string goalToBindTo = eventBinding.GoalToBindTo.ToLower().Replace("!", "");

			// GoalToBindTo = Hello
			if (!goalToBindTo.Contains(".") && !goalToBindTo.Contains("*") && !goalToBindTo.Contains("/") && !goalToBindTo.Contains(@"\") && !goalToBindTo.Contains(":"))
			{
				if (goalToBindTo.StartsWith("^") || goalToBindTo.EndsWith("$"))
				{
					return Regex.IsMatch(goal.GoalName, goalToBindTo, RegexOptions.IgnoreCase);
				}
				return goal.GoalName.ToLower() == goalToBindTo;
			}

			// GoalToBindTo = Hello.goal
			if (goalToBindTo.Contains(".") && Path.GetExtension(goalToBindTo) == ".goal")
			{
				return goal.GoalFileName.ToLower() == goalToBindTo || goal.RelativeGoalPath.ToLower() == goalToBindTo;
			}

			if (goalToBindTo.Contains("*"))
			{
				return IsMatchingStarPattern(goal, goalToBindTo);
			}

			if (goalToBindTo.Contains(":"))
			{
				string[] bindings = goalToBindTo.Split(":", StringSplitOptions.RemoveEmptyEntries);
				string goalFileName = Path.GetExtension(bindings[0]) == ".goal" ? bindings[0] : bindings[0] + ".goal";
				goalFileName = ChangeDirectorySeperators(goalFileName);

				return goal.RelativeGoalPath.ToLower() == goalFileName && goal.GoalName.ToLower() == bindings[1].ToLower();
			}

			// GoalToBindTo = AppName.StepName
			if (goalToBindTo.Contains(".") && goal.AppName != Path.DirectorySeparatorChar.ToString())
			{
				string[] bindings = goalToBindTo.Split(".", StringSplitOptions.RemoveEmptyEntries);
				if (goal.AppName.ToLower() != bindings[0].ToLower()) return false;

				return goal.GoalName.ToLower() == bindings[1].ToLower();
			}


			return false;
		}

		private string ChangeDirectorySeperators(string path)
		{
			path = path.Replace(@"\", @"/");
			if (!path.StartsWith(@"/")) path = @"/" + path;
			if (path == "/*")
			{
				path = path.Replace("*", ".*");
			}

			return path.ToLower();
		}

		private bool IsMatchingStarPattern(Goal goal, string goalToBindTo)
		{

			goalToBindTo = ChangeDirectorySeperators(goalToBindTo);
			var goalRelativeFolderPath = ChangeDirectorySeperators(goal.RelativeGoalFolderPath);
			if (!goalRelativeFolderPath.EndsWith("/")) goalRelativeFolderPath += "/";

			return Regex.IsMatch(goalRelativeFolderPath, @"^" + goalToBindTo + "$");

		}


	}

}
