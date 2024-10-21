using LightInject;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;
using Newtonsoft.Json;
using PLang.Building.Model;
using PLang.Building.Parsers;
using PLang.Container;
using PLang.Errors;
using PLang.Errors.Builder;
using PLang.Errors.Events;
using PLang.Errors.Handlers;
using PLang.Errors.Runtime;
using PLang.Exceptions;
using PLang.Interfaces;
using PLang.Runtime;
using PLang.SafeFileSystem;
using PLang.Utils;
using RazorEngineCore;
using System.IO;
using System.Text.RegularExpressions;

namespace PLang.Events
{

	public interface IEventRuntime
	{
		Task<List<EventBinding>> GetBuilderEvents();
		Task<List<EventBinding>> GetRuntimeEvents();
		(List<string> EventFiles, IError? Error) GetEventsFiles(string buildPath, bool builder = false);
		bool GoalHasBinding(Goal goal, EventBinding eventBinding);
		bool IsStepMatch(GoalStep step, EventBinding eventBinding);
		Task<IError> Load(bool builder = false);
		Task<IBuilderError?> RunBuildGoalEvents(string eventType, Goal goal);
		Task<IBuilderError?> RunBuildStepEvents(string eventType, Goal goal, GoalStep step, int stepIdx);
		Task<IEventError?> RunGoalEvents(PLangAppContext context, string eventType, Goal goal, bool isBuilder = false);
		Task<IEventError?> RunStartEndEvents(PLangAppContext context, string eventType, string eventScope, bool isBuilder = false);
		Task<IEventError?> RunStepEvents(PLangAppContext context, string eventType, Goal goal, GoalStep step, bool isBuilder = false);
		Task<IError?> RunOnErrorStepEvents(PLangAppContext context, IError error, Goal goal, GoalStep step);
		Task<IError?> RunGoalErrorEvents(PLangAppContext context, Goal goal, int goalStepIndex, IError error);
		Task<IError?> AppErrorEvents(PLangAppContext context, IError error);
		void SetContainer(IServiceContainer container);
	}
	public class EventRuntime : IEventRuntime
	{
		private readonly IPLangFileSystem fileSystem;
		private readonly ISettings settings;
		private readonly IPseudoRuntime pseudoRuntime;
		private readonly PrParser prParser;
		private readonly IEngine engine;
		private readonly IErrorHandlerFactory errorHandlerFactory;
		private readonly IErrorSystemHandlerFactory errorSystemHandlerFactory;
		private readonly ILogger logger;
		private readonly IFileAccessHandler fileAccessHandler;
		private List<EventBinding>? runtimeEvents = null;
		private List<EventBinding>? bulderEvents = null;
		private IServiceContainer? container;

		public EventRuntime(IPLangFileSystem fileSystem, ISettings settings, IPseudoRuntime pseudoRuntime,
			PrParser prParser, IEngine engine, IErrorHandlerFactory errorHandlerFactory, IErrorSystemHandlerFactory errorSystemHandlerFactory,
			ILogger logger, IFileAccessHandler fileAccessHandler)
		{
			this.fileSystem = fileSystem;
			this.settings = settings;
			this.pseudoRuntime = pseudoRuntime;
			this.prParser = prParser;
			this.engine = engine;
			this.errorHandlerFactory = errorHandlerFactory;
			this.errorSystemHandlerFactory = errorSystemHandlerFactory;
			this.logger = logger;
			this.fileAccessHandler = fileAccessHandler;
		}

		public void SetContainer(IServiceContainer container)
		{
			this.container = container;
		}

		public async Task<List<EventBinding>> GetBuilderEvents()
		{
			if (bulderEvents == null)
			{
				throw new BuilderException("Events are null. GetBuilderEvents() cannot be called before Load");
			}
			return bulderEvents!;
		}

		public async Task<List<EventBinding>> GetRuntimeEvents()
		{
			if (runtimeEvents == null)
			{
				throw new RuntimeException("Events are null. GetRuntimeEvents() cannot be called before Load");
			}
			return runtimeEvents!;
		}
		public async Task<IError?> Load(bool builder = false)
		{
			var events = new List<EventBinding>();

			(var eventsFiles, var error) = GetEventsFiles(fileSystem.BuildPath, builder);

			if (error != null) return error;
			if (eventsFiles == null) return null;

			foreach (var eventFile in eventsFiles)
			{
				var goal = prParser.GetGoal(eventFile);
				if (goal == null)
				{
					continue;
				}
				foreach (var step in goal.GoalSteps)
				{
					if (step.EventBinding == null) continue;

					// dont run event that is meant for debug mode
					if (!ShouldRunEvent(step.EventBinding.RunOnlyOnStartParameter))
					{
						continue;
					}
					events.Add(step.EventBinding);
				}

				if (container != null && goal.Injections != null)
				{
					foreach (var injection in goal.Injections)
					{
						container.RegisterForPLangUserInjections(injection.Type, injection.Path, injection.IsGlobal, injection.EnvironmentVariable, injection.EnvironmentVariableValue);
					}
				}
			}
			logger.LogDebug("Loaded {0} events", events.Count);
			// todo: wtf Ingi?
			if (builder)
			{
				bulderEvents = events;
			}
			else
			{
				runtimeEvents = events;
			}

			prParser.ForceLoadAllGoals();
			return null;
		}

		private bool ShouldRunEvent(string[]? runOnlyOnStartArguments)
		{
			if (runOnlyOnStartArguments == null || runOnlyOnStartArguments.Length == 0) return true;

			var startArgs = AppContext.GetData(ReservedKeywords.ParametersAtAppStart) as string[];
			if (startArgs == null) return false;

			foreach (var arg in runOnlyOnStartArguments)
			{
				if (startArgs.FirstOrDefault(p => p.Equals(arg, StringComparison.OrdinalIgnoreCase)) != null)
				{
					return true;
				}
			}
			return false;
		}

		public (List<string> EventFiles, IError? Error) GetEventsFiles(string buildPath, bool builder = false)
		{
			if (!fileSystem.Directory.Exists(buildPath))
			{
				return ([], new Error(".build folder does not exists. Run 'plang build' first."));
			}

			List<string> eventFiles = new();
			string eventsFolderName = (!builder) ? "Events" : "BuilderEvents";

			CopyPlangEvents();
			var eventsFolderPath = fileSystem.Path.Join(buildPath, "events");

			if (!fileSystem.Directory.Exists(eventsFolderPath)) return (eventFiles, null);

			var rootEventsFolderPath = fileSystem.Path.Join(eventsFolderPath, eventsFolderName);
			var rootEventFilePath = fileSystem.Path.Join(rootEventsFolderPath, "00. Goal.pr");
			var rootEventFileExists = fileSystem.File.Exists(rootEventFilePath);


			var externalEventsPath = fileSystem.Path.Join(eventsFolderPath, "external");
			if (fileSystem.Directory.Exists(externalEventsPath))
			{
				var externalEventsDirectories = fileSystem.Directory.GetDirectories(externalEventsPath, eventsFolderName, SearchOption.AllDirectories);

				eventFiles.AddRange(externalEventsDirectories.SelectMany(dir => fileSystem.Directory.GetFiles(dir, "00. Goal.pr"))
									 .ToList());
			}




			if (rootEventFileExists)
			{
				eventFiles.Add(rootEventFilePath);
			}
			return (eventFiles, null);

		}

		private void CopyPlangEvents()
		{
			string plangGoalsPath = fileSystem.Path.Join(fileSystem.RootDirectory, "events/external/plang");
			if (fileSystem.Directory.Exists(plangGoalsPath)) return;

			var plangFolder = AppContext.BaseDirectory;
			string plangEventsFolder = fileSystem.Path.Join(plangFolder, "Goals/events").AdjustPathToOs();
			string plangBuildEventsFolder = fileSystem.Path.Join(plangFolder, "Goals/.build/events").AdjustPathToOs();
			 
			var task = fileAccessHandler.ValidatePathResponse(fileSystem.RootDirectory, plangEventsFolder, "y");
			task.Wait();
			(var success, var error) = task.Result;
			

			DirectoryHelper.Copy(plangEventsFolder, fileSystem.Path.Join(fileSystem.RootDirectory, "events").AdjustPathToOs());
			DirectoryHelper.Copy(plangBuildEventsFolder, fileSystem.Path.Join(fileSystem.RootDirectory, ".build/events").AdjustPathToOs());
			prParser.ForceLoadAllGoals();

		}

		public async Task<IEventError?> RunStartEndEvents(PLangAppContext context, string eventType, string eventScope, bool isBuilder = false)
		{
			runtimeEvents = (isBuilder) ? await GetBuilderEvents() : await GetRuntimeEvents();

			if (runtimeEvents == null || context.ContainsKey(ReservedKeywords.IsEvent)) return null;

			List<EventBinding> eventsToRun = runtimeEvents.Where(p => p.EventScope == eventScope).ToList();

			for (var i = 0; i < eventsToRun.Count; i++)
			{
				var eve = eventsToRun[i];
				var parameters = new Dictionary<string, object?>();
				parameters.Add(ReservedKeywords.Event, eve);
				context.Add(ReservedKeywords.IsEvent, true);

				logger.LogDebug("Run event type {0} on scope {1}, binding to {2} calling {3}", eventType, eventScope, eve.GoalToBindTo, eve.GoalToCall);
				var task = pseudoRuntime.RunGoal(engine, context, fileSystem.Path.DirectorySeparatorChar.ToString(), eve.GoalToCall, parameters);
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

				if (error == null) continue;

				if (isBuilder)
				{
					return new BuilderEventError(error.Message, eve, InitialError: error);
				}
				return new RuntimeEventError(error.Message, eve, InitialError: error);
			}
			return null;

		}


		public async Task<IError?> AppErrorEvents(PLangAppContext context, IError error)
		{
			var eventsToRun = runtimeEvents.Where(p => p.EventScope == EventScope.AppError).ToList();
			if (eventsToRun.Count > 0)
			{
				foreach (var eve in runtimeEvents)
				{
					var runError = await Run(context, eve, null, null, error);
					if (runError != null) return runError;
				}
			}
			else
			{
				return error;
				//await ShowDefaultError(error, null);
			}
			return null;

		}

		private async Task<IError?> HandleError(PLangAppContext context, Goal goal, IError error, GoalStep? step, List<EventBinding> eventsToRun)
		{
			if (eventsToRun.Count == 0) return error;

			bool hasHandled = false;
			for (var i = 0; i < eventsToRun.Count; i++)
			{
				var eve = eventsToRun[i];
				if (!GoalHasBinding(goal, eve)) continue;

				var errorRun = await Run(context, eve, goal, step, error);
				if (errorRun != null) return errorRun;
				hasHandled = true;
			}
			return (hasHandled) ? new ErrorHandled(error) : error;
		}

		public async Task<IBuilderError?> RunBuildGoalEvents(string eventType, Goal goal)
		{

			var events = await GetBuilderEvents();
			if (events.Count == 0) return null;

			PLangAppContext context = new();
			context.Add(ReservedKeywords.Goal, goal);

			//when EventBuildBinding exists, then new RunGoalBuildEvents needs to be created that return IBuilderError, RunGoalEvents return IError
			var error = await RunGoalEvents(context, eventType, goal, true);
			return error as IBuilderError;
		}

		public async Task<IEventError?> RunGoalEvents(PLangAppContext context, string eventType, Goal goal, bool isBuilder = false)
		{
			if (runtimeEvents == null || context.ContainsKey(ReservedKeywords.IsEvent)) return null;
			var eventsToRun = runtimeEvents.Where(p => p.EventType == eventType && p.EventScope == EventScope.Goal).ToList();
			for (var i = 0; i < eventsToRun.Count; i++)
			{
				var eve = eventsToRun[i];
				if (!GoalHasBinding(goal, eve)) continue;

				var error = await Run(context, eve, goal, isBuilder: isBuilder);
				if (error != null) return error;
			}
			return null;
		}

		public async Task<IError?> RunGoalErrorEvents(PLangAppContext context, Goal goal, int goalStepIndex, IError error)
		{
			if (runtimeEvents == null || context.ContainsKey(ReservedKeywords.IsEvent)) return error;

			var step = (goalStepIndex != -1 && goalStepIndex < goal.GoalSteps.Count) ? goal.GoalSteps[goalStepIndex] : null;
			var eventsToRun = runtimeEvents.Where(p => p.EventScope == EventScope.GoalError).ToList();

			return await HandleError(context, goal, error, step, eventsToRun);

		}

		private async Task<IEventError?> Run(PLangAppContext context, EventBinding eve, Goal? goal, GoalStep? step = null, IError? error = null, bool isBuilder = false)
		{
			try
			{
				var parameters = new Dictionary<string, object?>();
				parameters.Add(ReservedKeywords.Event, eve);
				parameters.Add(ReservedKeywords.Goal, goal);
				parameters.Add(ReservedKeywords.Error, error);
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
					if (isBuilder)
					{
						return new BuilderEventError(exception.Message, eve, goal, step, Exception: exception);
					}
					return new RuntimeEventError(exception.Message, eve, goal, step, Exception: exception);
				}
				if (task.Result.error == null) return null;
				if (task.Result.error is IErrorHandled eh) return eh;
				if (task.Result.error is UserDefinedError ude) return ude;

				if (isBuilder)
				{
					return new BuilderEventError(task.Result.error.Message, eve, goal, step, InitialError: task.Result.error);
				}
				return new RuntimeEventError(task.Result.error.Message, eve, goal, step, InitialError: task.Result.error);
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
		public async Task<IBuilderError?> RunBuildStepEvents(string eventType, Goal goal, GoalStep step, int stepIdx)
		{
			var context = new PLangAppContext();
			context.Add(ReservedKeywords.Goal, goal);
			context.Add(ReservedKeywords.Step, step);
			context.Add(ReservedKeywords.StepIndex, stepIdx);

			var error = await RunStepEvents(context, eventType, goal, step, true);
			return error as IBuilderError;
		}


		public async Task<IEventError?> RunStepEvents(PLangAppContext context, string eventType, Goal goal, GoalStep step, bool isBuilder = false)
		{
			if (runtimeEvents == null || context.ContainsKey(ReservedKeywords.IsEvent))
			{
				return null;
			}

			var eventsToRun = runtimeEvents.Where(p => p.EventType == eventType && p.EventScope == EventScope.Step).ToList();
			for (var i = 0; i < eventsToRun.Count; i++)
			{
				var eve = eventsToRun[i];
				if (GoalHasBinding(goal, eve) && IsStepMatch(step, eve))
				{
					return await Run(context, eve, goal, step, isBuilder: isBuilder);
				}
			}
			return null;
		}
		public async Task<IError?> RunOnErrorStepEvents(PLangAppContext context, IError error, Goal goal, GoalStep step)
		{
			if (runtimeEvents == null || context.ContainsKey(ReservedKeywords.IsEvent)) return error;
			if (error is EndGoal)
			{
				return error;
			}

			List<EventBinding> eventsToRun = new();
			eventsToRun.AddRange(runtimeEvents.Where(p => p.EventType == EventType.Before && p.EventScope == EventScope.StepError).ToList());



			var errorHandler = StepHelper.GetErrorHandlerForStep(step.ErrorHandlers, error);
			if (errorHandler != null)
			{
				if (errorHandler.GoalToCall != null && !string.IsNullOrEmpty(errorHandler.GoalToCall))
				{
					var eventBinding = new EventBinding(EventType.Before, EventScope.StepError, goal.RelativeGoalPath, errorHandler.GoalToCall, true, step.Number, step.Text, true, null, errorHandler.IgnoreError);
					eventsToRun.Add(eventBinding);
				}
				else if (errorHandler.IgnoreError)
				{
					return null;
				}
			}

			eventsToRun.AddRange(runtimeEvents.Where(p => p.EventType == EventType.After && p.EventScope == EventScope.StepError).ToList());

			if (eventsToRun.Count == 0)
			{
				if (goal.ParentGoal != null) return error;
				return error;
			}
			else
			{
				bool shouldContinueNextStep = false;
				var multiError = new MultipleError(error);
				foreach (var eve in eventsToRun)
				{
					if (GoalHasBinding(goal, eve) && IsStepMatch(step, eve))
					{
						var eventError = await Run(context, eve, goal, step, error);
						if (eventError != null)
						{
							multiError.Add(eventError);
						}
					}

					//comment: this might be a bad idea, what happens when you have multiple events, on should continue other not
					if (!shouldContinueNextStep) shouldContinueNextStep = eve.OnErrorContinueNextStep;

				}
				if (shouldContinueNextStep) return null;
				if (multiError.Errors.Count == 0) return multiError.InitialError;

				var endGoal = multiError.Errors.FirstOrDefault(p => p is EndGoal);
				if (endGoal != null) { return endGoal; }

			}

			return error;
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

			string goalToBindTo = eventBinding.GoalToBindTo.ToString().ToLower().Replace("!", "");

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
			if (goalToBindTo.Contains(".") && fileSystem.Path.GetExtension(goalToBindTo) == ".goal")
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
				string goalFileName = fileSystem.Path.GetExtension(bindings[0]) == ".goal" ? bindings[0] : bindings[0] + ".goal";
				goalFileName = ChangeDirectorySeperators(goalFileName);

				return goal.RelativeGoalPath.ToLower() == goalFileName && goal.GoalName.ToLower() == bindings[1].ToLower();
			}

			// GoalToBindTo = AppName.StepName
			if (goalToBindTo.Contains(".") && goal.AppName != fileSystem.Path.DirectorySeparatorChar.ToString())
			{
				string[] bindings = goalToBindTo.Split(".", StringSplitOptions.RemoveEmptyEntries);
				if (goal.AppName.ToLower() != bindings[0].ToLower()) return false;

				return goal.GoalName.ToLower() == bindings[1].ToLower();
			}


			return false;
		}

		private string ChangeDirectorySeperators(string path)
		{
			path = path.TrimStart('^');
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
