﻿using LightInject;
using Microsoft.Extensions.Logging;
using PLang.Building.Model;
using PLang.Building.Parsers;
using PLang.Container;
using PLang.Errors;
using PLang.Errors.Builder;
using PLang.Errors.Events;
using PLang.Errors.Runtime;
using PLang.Exceptions;
using PLang.Interfaces;
using PLang.Runtime;
using PLang.Utils;
using System.Collections.Concurrent;
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
		Task<IEventError?> RunGoalEvents(string eventType, Goal goal, bool isBuilder = false);
		Task<IEventError?> RunStartEndEvents(string eventType, string eventScope, bool isBuilder = false);
		Task<IEventError?> RunStepEvents(string eventType, Goal goal, GoalStep step, bool isBuilder = false);
		Task<IError?> RunOnErrorStepEvents(IError error, Goal goal, GoalStep step);
		Task<IError?> RunGoalErrorEvents(Goal goal, int goalStepIndex, IError error);
		Task<IError?> AppErrorEvents(IError error);
		void SetContainer(IServiceContainer container);
		void SetActiveEvents(ConcurrentDictionary<string, string> activeEvents);
		ConcurrentDictionary<string, string> GetActiveEvents();
		Task<IError?> LoadBuilder(MemoryStack memoryStack);
	}
	public class EventRuntime : IEventRuntime
	{
		private readonly IPLangFileSystem fileSystem;
		private readonly IPseudoRuntime pseudoRuntime;
		private readonly PrParser prParser;
		private readonly IEngine engine;
		private readonly ILogger logger;
		private List<EventBinding>? runtimeEvents = null;
		private List<EventBinding>? builderEvents = null;
		private IServiceContainer? container;
		private ConcurrentDictionary<string, string> ActiveEvents;
		public EventRuntime(IPLangFileSystem fileSystem, IPseudoRuntime pseudoRuntime,
			PrParser prParser, IEngine engine,  ILogger logger)
		{
			this.fileSystem = fileSystem;
			this.pseudoRuntime = pseudoRuntime;
			this.prParser = prParser;
			this.engine = engine;
			this.logger = logger;
			this.ActiveEvents = new();
		}

		public void SetContainer(IServiceContainer container)
		{
			this.container = container;
		}

		public void SetActiveEvents(ConcurrentDictionary<string, string> activeEvents)
		{
			this.ActiveEvents = activeEvents;
		}

		public ConcurrentDictionary<string, string> GetActiveEvents()
		{
			return this.ActiveEvents;
		}

		public async Task<List<EventBinding>> GetBuilderEvents()
		{
			if (builderEvents == null)
			{
				throw new BuilderException("Events are null. GetBuilderEvents() cannot be called before Load");
			}
			return builderEvents!;
		}

		public async Task<List<EventBinding>> GetRuntimeEvents()
		{
			if (runtimeEvents == null)
			{
				throw new RuntimeException("Events are null. GetRuntimeEvents() cannot be called before Load");
			}
			return runtimeEvents!;
		}

		public async Task<IError?> LoadBuilder(MemoryStack memoryStack)
		{
			var result = await Load(true);
			foreach (var item in memoryStack.GetMemoryStack())
			{
				engine.GetMemoryStack().Put(item.Value);
			}
			
			return result;
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

					var eventBinding = events.FirstOrDefault(p => p == step.EventBinding);
					if (eventBinding != null) continue;

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
				builderEvents = events;
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
				return ([], new Error($".build folder does not exists. Run 'plang build' first. Searching at {buildPath}"));
			}

			List<string> eventFiles = new();
			string eventsFolderName = (!builder) ? "Events" : "BuilderEvents";

			var eventsFolderPath = fileSystem.Path.Join(buildPath, "events", eventsFolderName);
			var rootEventFilePath = fileSystem.Path.Join(eventsFolderPath, "00. Goal.pr");
			if (fileSystem.File.Exists(rootEventFilePath))
			{
				eventFiles.Add(rootEventFilePath);
			}

			var osEventsPath = fileSystem.Path.Join(fileSystem.OsDirectory, ".build", "events", eventsFolderName);
			var osEventFilePath = fileSystem.Path.Join(osEventsPath, "00. Goal.pr");
			if (fileSystem.File.Exists(osEventFilePath))
			{
				eventFiles.Add(osEventFilePath);
			}

			return (eventFiles, null);

		}

		public async Task<IEventError?> RunStartEndEvents(string eventType, string eventScope, bool isBuilder = false)
		{
			var events = (isBuilder) ? await GetBuilderEvents() : await GetRuntimeEvents();
			var context = engine.GetContext();

			if (events == null)
			{
				return null;
			}

			List<EventBinding> eventsToRun = events.Where(p => p.EventScope == eventScope).ToList();

			for (var i = 0; i < eventsToRun.Count; i++)
			{
				var eve = eventsToRun[i];
				if (ActiveEvents.ContainsKey(eve.Id)) continue;

				var parameters = new Dictionary<string, object?>();
				parameters.Add(ReservedKeywords.Event, eve);
				context.AddOrReplace(ReservedKeywords.IsEvent, true);
				parameters.Add("!plang.EventUniqueId", Guid.NewGuid().ToString());

				ActiveEvents.TryAdd(eve.Id, eve.GoalToCall);
				logger.LogDebug("Run event type {0} on scope {1}, binding to {2} calling {3}", eventType, eventScope, eve.GoalToBindTo, eve.GoalToCall);
				var task = pseudoRuntime.RunGoal(engine, context, "events", eve.GoalToCall, parameters);
				if (eve.WaitForExecution)
				{
					await task;
				}
				(_, var error, var output) = task.Result;
				ActiveEvents.Remove(eve.Id, out _);

				context.Remove(ReservedKeywords.IsEvent);
				if (context.TryGetValue(ReservedKeywords.MemoryStack, out var obj0) && obj0 != null)
				{
					var memoryStack = (MemoryStack)obj0;
					memoryStack.Remove(ReservedKeywords.Event);
				}

				if (error == null) continue;
				if (error is RuntimeEventError ree) return ree;
				if (error is BuilderEventError bee) return bee;

				if (isBuilder)
				{
					return new BuilderEventError(error.Message, eve, InitialError: error);
				}
				return new RuntimeEventError(error.Message, eve, InitialError: error);
			}
			return null;

		}


		public async Task<IError?> AppErrorEvents(IError error)
		{
			if (runtimeEvents == null) return error;

			var eventsToRun = runtimeEvents.Where(p => p.EventScope == EventScope.AppError).ToList();
			if (eventsToRun.Count > 0)
			{
				foreach (var eve in runtimeEvents)
				{
					if (!HasAppBinding(eve, error)) continue;
					return await Run(eve, null, null, error);
					//if (runError != null) return runError;
				}
			}
			else
			{
				return error;
				//await ShowDefaultError(error, null);
			}
			return null;

		}

		private bool HasAppBinding(EventBinding eve, IError error)
		{
			if (eve.ErrorKey != null && !eve.ErrorKey.Equals(error.Key, StringComparison.OrdinalIgnoreCase)) return false;
			if (eve.ErrorMessage != null && !error.Message.Contains(eve.ErrorMessage, StringComparison.OrdinalIgnoreCase)) return false;	
			if (eve.StatusCode != null && eve.StatusCode != error.StatusCode) return false;
			if (eve.ExceptionType != null && !eve.ExceptionType.Equals(error.Exception?.GetType().FullName)) return false;
			if (eve.GoalToBindTo == "*") return true;

			return false;
		}

		private async Task<IError?> HandleGoalError(Goal goal, IError error, GoalStep? step, List<EventBinding> eventsToRun)
		{
			if (eventsToRun.Count == 0) return error;

			bool hasHandled = false;

			for (var i = 0; i < eventsToRun.Count; i++)
			{
				var eve = eventsToRun[i];
				if (ActiveEvents.ContainsKey(eve.Id)) continue;
				if (!GoalHasBinding(goal, eve)) continue;

				var errorRun = await Run(eve, goal, step, error);
				if (errorRun != null) return errorRun;
				hasHandled = true;
			}
			return (hasHandled) ? new ErrorHandled(error) : error;
		}

		public async Task<IBuilderError?> RunBuildGoalEvents(string eventType, Goal goal)
		{

			var events = await GetBuilderEvents();
			if (events.Count == 0) return null;

			var context = engine.GetContext();
			context.AddOrReplace(ReservedKeywords.Goal, goal);

			//when EventBuildBinding exists, then new RunGoalBuildEvents needs to be created that return IBuilderError, RunGoalEvents return IError
			var error = await RunGoalEvents(eventType, goal, true);
			return error as IBuilderError;
		}

		public async Task<IEventError?> RunGoalEvents(string eventType, Goal goal, bool isBuilder = false)
		{
			var context = engine.GetContext();
			
			var events = (isBuilder) ? builderEvents : runtimeEvents;
			if (events == null)
			{
				return null;
			}
			var eventsToRun = events.Where(p => p.EventType == eventType && p.EventScope == EventScope.Goal).ToList();
			for (var i = 0; i < eventsToRun.Count; i++)
			{
				var eve = eventsToRun[i];
				if (ActiveEvents.ContainsKey(eve.Id)) continue;
				if (!GoalHasBinding(goal, eve)) continue;

				var error = await Run(eve, goal, isBuilder: isBuilder);
				if (error != null) return error;
			}
			return null;
		}

		public async Task<IError?> RunGoalErrorEvents(Goal goal, int goalStepIndex, IError error)
		{
			var context = engine.GetContext();
			if (runtimeEvents == null)
			{
				return error;
			}


			var step = (goalStepIndex != -1 && goalStepIndex < goal.GoalSteps.Count) ? goal.GoalSteps[goalStepIndex] : null;
			var eventsToRun = runtimeEvents.Where(p => p.EventScope == EventScope.GoalError).ToList();

			return await HandleGoalError(goal, error, step, eventsToRun);

		}

		private async Task<IEventError?> Run(EventBinding eve, Goal? callingGoal = null, GoalStep? step = null, IError? error = null, bool isBuilder = false)
		{
			var context = engine.GetContext();
			try
			{
				context.TryGetValue(ReservedKeywords.CallingGoal, out object? prevCallingGoal);
				context.TryGetValue(ReservedKeywords.CallingStep, out object? prevCallingStep);


				var parameters = new Dictionary<string, object?>();
				parameters.Add(ReservedKeywords.Event, eve);
				parameters.Add(ReservedKeywords.CallingGoal, callingGoal);
				if (error != null) parameters.Add(ReservedKeywords.Error, error);
				if (error is ErrorHandled) parameters.Add(ReservedKeywords.Error, error);
				parameters.Add("!plang.EventUniqueId", Guid.NewGuid().ToString());

				context.TryAdd(ReservedKeywords.IsEvent, true);

				if (step != null) parameters.Add(ReservedKeywords.CallingStep, step);
				string path = (eve.IsLocal) ? callingGoal?.RelativeGoalFolderPath : "/events";

				logger.LogDebug("Run event type {0} on scope {1}, binding to {2} calling {3}", eve.EventType.ToString(), eve.EventScope.ToString(), eve.GoalToBindTo, eve.GoalToCall);
				ActiveEvents.TryAdd(eve.Id, eve.GoalToCall);
				var task = pseudoRuntime.RunGoal(engine, context, path, eve.GoalToCall, parameters, isolated: !eve.IsLocal);
				if (eve.WaitForExecution)
				{
					await task;
				}
				ActiveEvents.Remove(eve.Id, out _);

				context.AddOrReplace(ReservedKeywords.CallingGoal, prevCallingGoal);
				context.AddOrReplace(ReservedKeywords.CallingStep, prevCallingStep);

				if (task.Exception != null)
				{
					var exception = task.Exception.InnerException ?? task.Exception;
					if (isBuilder)
					{
						return new BuilderEventError(exception.Message, eve, callingGoal, step, Exception: exception);
					}
					return new RuntimeEventError(exception.Message, eve, callingGoal, step, Exception: exception);
				}
				if (task.Result.error == null) return null;
				if (task.Result.error is IErrorHandled eh) return eh;
				if (task.Result.error is UserDefinedError ude) return ude;

				if (isBuilder)
				{
					return new BuilderEventError(task.Result.error.Message, eve, callingGoal, step, InitialError: task.Result.error);
				}
				return new RuntimeEventError(task.Result.error.Message, eve, callingGoal, step, InitialError: task.Result.error);
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
			var context = engine.GetContext();
			context.AddOrReplace(ReservedKeywords.Goal, goal);
			context.AddOrReplace(ReservedKeywords.Step, step);
			context.AddOrReplace(ReservedKeywords.StepIndex, stepIdx);

			var error = await RunStepEvents(eventType, goal, step, true);
			return error as IBuilderError;
		}


		public async Task<IEventError?> RunStepEvents(string eventType, Goal goal, GoalStep step, bool isBuilder = false)
		{
			var events = (isBuilder) ? await GetBuilderEvents() : await GetRuntimeEvents();

			var context = engine.GetContext();
			if (events == null)
			{
				return null;
			}

			var eventsToRun = events.Where(p => p.EventType == eventType && p.EventScope == EventScope.Step).ToList();
			for (var i = 0; i < eventsToRun.Count; i++)
			{
				var eve = eventsToRun[i];
				if (ActiveEvents.ContainsKey(eve.Id)) continue;
				if (GoalHasBinding(goal, eve) && IsStepMatch(step, eve))
				{
					return await Run(eve, goal, step, isBuilder: isBuilder);
				}
			}
			return null;
		}
		public async Task<IError?> RunOnErrorStepEvents(IError error, Goal goal, GoalStep step)
		{
			var context = engine.GetContext();
			if (runtimeEvents == null)
			{
				return error;
			}
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
					var eventBinding = new EventBinding(EventType.Before, EventScope.StepError, goal.RelativeGoalPath, errorHandler.GoalToCall,
						true, step.Number, step.Text, true, null, errorHandler.IgnoreError, errorHandler.Key, errorHandler.Message, errorHandler.StatusCode, IsLocal: true);
					
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
				foreach (var eve in eventsToRun)
				{
					if (ActiveEvents.ContainsKey(eve.Id)) continue;
					if (GoalHasBinding(goal, eve) && IsStepMatch(step, eve) && EventMatchesError(eve, error))
					{
						var eventError = await Run(eve, goal, step, error);
						if (eventError != null) return eventError;

						if (eve.OnErrorContinueNextStep) return null;

						return new ErrorHandled(error);
					}

				}
			}

			return error;
		}

		private bool EventMatchesError(EventBinding eve, IError error)
		{
			if (eve.ErrorKey != null && eve.ErrorKey.Equals(error.Key, StringComparison.OrdinalIgnoreCase)) return true;
			if (eve.ErrorMessage != null && error.Message.Contains(eve.ErrorMessage, StringComparison.OrdinalIgnoreCase)) return true;
			if (eve.StatusCode != null && eve.StatusCode == error.StatusCode) return true;
			if (eve.ExceptionType != null && Type.GetType(eve.ExceptionType) == error.Exception?.GetType()) return true;

			return (string.IsNullOrEmpty(eve.ErrorKey) && string.IsNullOrEmpty(eve.ErrorMessage) && string.IsNullOrEmpty(eve.ExceptionType) && eve.StatusCode == null);
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
			if (step.Goal.IsOS && !eventBinding.IncludeOsGoals) return false;

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
			if (!eventBinding.IsLocal && goal.IsOS && !eventBinding.IncludeOsGoals) return false;

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
