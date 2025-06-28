using LightInject;
using Microsoft.Extensions.Logging;
using PLang.Building.Model;
using PLang.Building.Parsers;
using PLang.Container;
using PLang.Errors;
using PLang.Errors.Builder;
using PLang.Errors.Events;
using PLang.Errors.Interfaces;
using PLang.Errors.Runtime;
using PLang.Exceptions;
using PLang.Interfaces;
using PLang.Models;
using PLang.Modules.CallGoalModule;
using PLang.Runtime;
using PLang.Utils;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Reflection;
using System.Text.RegularExpressions;
using static PLang.Utils.VariableHelper;

namespace PLang.Events
{

	public interface IEventRuntime
	{
		Task<List<EventBinding>> GetBuilderEvents();
		Task<List<EventBinding>> GetRuntimeEvents();
		(List<string> EventFiles, IError? Error) GetEventsFiles(string buildPath, bool builder = false);
		bool GoalHasBinding(Goal goal, EventBinding eventBinding);
		bool IsStepMatch(GoalStep step, EventBinding eventBinding);
		IError? Load(bool builder = false);
		Task<(object? Variables, IBuilderError? Error)> RunBuildGoalEvents(string eventType, Goal goal);
		Task<(object? Variables, IBuilderError? Error)> RunBuildStepEvents(string eventType, Goal goal, GoalStep step, int stepIdx);
		Task<(object? Variables, IError? Error)> RunGoalEvents(string eventType, Goal goal, bool isBuilder = false);
		Task<(object? Variables, IError? Error)> RunStartEndEvents(string eventType, string eventScope, bool isBuilder = false);
		Task<(object? Variables, IError? Error)> RunStepEvents(string eventType, Goal goal, GoalStep step, bool isBuilder = false);
		Task<(object? Variables, IError? Error)> RunOnErrorStepEvents(IError error, Goal goal, GoalStep step, bool isBuilder = false);
		Task<(object? Variables, IError? Error)> RunGoalErrorEvents(Goal goal, int goalStepIndex, IError error, bool isBuilder = false);
		Task<(object? Variables, IError? Error)> AppErrorEvents(IError error);
		void SetContainer(IServiceContainer container);
		void SetActiveEvents(ConcurrentDictionary<string, string> activeEvents);
		ConcurrentDictionary<string, string> GetActiveEvents();
		Task<(object? Variables, IError? Error)> RunOnModuleError(MethodInfo method, IError error, Exception ex);
	}

	public interface IBuilderEventRuntime : IEventRuntime { }

	public class BuilderEventRuntime : EventRuntime, IBuilderEventRuntime
	{
		public BuilderEventRuntime(IPLangFileSystem fileSystem, PrParser prParser, PLangAppContext context, ILogger logger, Program caller, MemoryStack memoryStack) : base(fileSystem, prParser, context, logger, caller, memoryStack)
		{
		}

		public IError? Load()
		{
			return base.Load(true);
		}
	}
	public class EventRuntime : IEventRuntime
	{
		private readonly IPLangFileSystem fileSystem;
		private readonly PrParser prParser;
		private readonly PLangAppContext context;
		private readonly ILogger logger;
		private readonly Modules.CallGoalModule.Program caller;
		private readonly MemoryStack memoryStack;
		private List<EventBinding>? runtimeEvents = null;
		private List<EventBinding>? builderEvents = null;
		private IServiceContainer? container;
		private ConcurrentDictionary<string, string> ActiveEvents;
		public EventRuntime(IPLangFileSystem fileSystem, PrParser prParser, PLangAppContext context, ILogger logger, Modules.CallGoalModule.Program caller, MemoryStack memoryStack)
		{
			this.fileSystem = fileSystem;
			this.prParser = prParser;
			this.context = context;
			this.logger = logger;
			this.caller = caller;
			this.memoryStack = memoryStack;
			this.ActiveEvents = new();
		}

		public void SetContainer(IServiceContainer container)
		{
			this.container = container;
			caller.Init(container, null, null, null, null);
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


		public IError? Load(bool isBuilder = false)
		{
			var events = new List<EventBinding>();

			(var eventsFiles, var error) = GetEventsFiles(fileSystem.BuildPath, isBuilder);

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

					var binding = step.EventBinding with { IsOnStep = false, IsLocal = !eventFile.StartsWith(fileSystem.Path.Join(fileSystem.OsDirectory, ".build")) };
					events.Add(binding);
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
			if (isBuilder)
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

		public async Task<(object? Variables, IError? Error)> RunStartEndEvents(string eventType, string eventScope, bool isBuilder = false)
		{
			var events = (isBuilder) ? await GetBuilderEvents() : await GetRuntimeEvents();
			if (events == null) return (null, null);

			List<EventBinding> eventsToRun = events.Where(p => p.EventScope == eventScope).ToList();

			for (var i = 0; i < eventsToRun.Count; i++)
			{
				var eve = eventsToRun[i];
				if (ActiveEvents.ContainsKey(eve.Id)) continue;

				var parameters = new Dictionary<string, object?>();
				eve.GoalToCall.Parameters.Add(ReservedKeywords.Event, eve);
				eve.GoalToCall.Parameters.Add(ReservedKeywords.IsEvent, true);
				eve.GoalToCall.Parameters.Add("!plang", new { EventUniqueId = Guid.NewGuid().ToString() });

				ActiveEvents.TryAdd(eve.Id, eve.GoalToCall.Name);
				logger.LogDebug("Run event type {0} on scope {1}, binding to {2} calling {3}", eventType, eventScope, eve.GoalToBindTo, eve.GoalToCall);

				var task = caller.RunGoal(eve.GoalToCall, isolated: !eve.IsLocal);
				if (eve.WaitForExecution)
				{
					await task;
				}
				var result = task.Result;
				ActiveEvents.Remove(eve.Id, out _);

				if (result.Error == null) continue;
				if (result.Error is RuntimeEventError ree) return result;
				if (result.Error is BuilderEventError bee) return result;

				if (isBuilder)
				{
					return (result.Return, new BuilderEventError(result.Error.Message, eve, InitialError: result.Error));
				}
				return (result.Return, new RuntimeEventError(result.Error.Message, eve, InitialError: result.Error));
			}
			return (null, null);

		}


		public async Task<(object? Variables, IError? Error)> AppErrorEvents(IError error)
		{
			if (runtimeEvents == null) return (null, error);

			var eventsToRun = runtimeEvents.Where(p => p.EventScope == EventScope.AppError).ToList();
			if (eventsToRun.Count > 0)
			{
				foreach (var eve in eventsToRun)
				{
					if (!HasAppBinding(eve, error)) continue;
					var result = await Run(eve, null, null, error);
					if (result.Error != null) return (null, new MultipleError(error).Add(result.Error));
				}
			}
			else
			{
				return (null, error);
				//await ShowDefaultError(error, null);
			}
			return (null, null);

		}

		private bool HasAppBinding(EventBinding eve, IError error)
		{
			if (eve.ErrorKey != null && !eve.ErrorKey.Equals(error.Key, StringComparison.OrdinalIgnoreCase)) return false;
			if (eve.ErrorMessage != null && !error.Message.Contains(eve.ErrorMessage, StringComparison.OrdinalIgnoreCase)) return false;
			if (eve.StatusCode != null && eve.StatusCode != error.StatusCode) return false;
			if (eve.ExceptionType != null && !eve.ExceptionType.Equals(error.Exception?.GetType().FullName)) return false;
			if (eve.GoalToBindTo.Name == "*") return true;

			return false;
		}

		private async Task<(object? Variables, IError? Error)> HandleGoalError(Goal goal, IError error, GoalStep? step, List<EventBinding> eventsToRun)
		{
			if (eventsToRun.Count == 0) return (null, error);

			bool hasHandled = false;

			List<ObjectValue?>? Variables = new();
			for (var i = 0; i < eventsToRun.Count; i++)
			{
				var eve = eventsToRun[i];
				if (ActiveEvents.ContainsKey(eve.Id)) continue;
				if (!GoalHasBinding(goal, eve) || !HasAppBinding(eve, error)) continue;

				var result = await Run(eve, goal, step, error);
				if (result.Error != null) return (result.Variables, new MultipleError(error).Add(result.Error));
				hasHandled = true;
				if (result.Variables != null) return (result.Variables, null);
			}

			return (hasHandled) ? (Variables, new ErrorHandled(error)) : (Variables, error);
		}

		private void ShowLogError(Goal? goal, GoalStep? step, IError error)
		{
			// when running in --debug or --csdebug mode write out to log an error happened
			string stepText = "";
			if (step != null)
			{
				stepText = $" at {step.Text.ReplaceLineEndings("").MaxLength(20, "...")}:{step.LineNumber}";
			}
			string goalText = "Error";
			if (goal != null)
			{
				goalText = $"Goal {goal.GoalFileName} had error";
			}
			logger.LogError($"[Error] - {goalText}{stepText} - {error.Message.ReplaceLineEndings("").MaxLength(40, "...")}");
		}

		public async Task<(object? Variables, IBuilderError? Error)> RunBuildGoalEvents(string eventType, Goal goal)
		{

			var events = await GetBuilderEvents();
			if (events.Count == 0) return (null, null);

			context.AddOrReplace(ReservedKeywords.Goal, goal);

			//when EventBuildBinding exists, then new RunGoalBuildEvents needs to be created that return IBuilderError, RunGoalEvents return IError
			var result = await RunGoalEvents(eventType, goal, true);
			if (result.Error != null)
			{
				return (result.Variables, new GoalBuilderError(result.Error, goal, false));
			}
			return (result.Variables, null);
		}

		public async Task<(object? Variables, IError? Error)> RunGoalEvents(string eventType, Goal goal, bool isBuilder = false)
		{
			var events = (isBuilder) ? builderEvents : runtimeEvents;
			if (events == null)
			{
				return (null, null);
			}
			var eventsToRun = events.Where(p => p.EventType == eventType && p.EventScope == EventScope.Goal).ToList();
			List<ObjectValue> Variables = new();
			for (var i = 0; i < eventsToRun.Count; i++)
			{
				var eve = eventsToRun[i];
				if (ActiveEvents.ContainsKey(eve.Id)) continue;
				if (!GoalHasBinding(goal, eve)) continue;

				var result = await Run(eve, goal, isBuilder: isBuilder);
				if (result.Error != null) return (Variables, result.Error);

				if (result.Variables != null) return (result.Variables, null);
			}
			return (Variables, null);
		}

		public async Task<(object? Variables, IError? Error)> RunGoalErrorEvents(Goal goal, int goalStepIndex, IError error, bool isBuilder = false)
		{
			var bindings = (isBuilder) ? builderEvents : runtimeEvents;
			if (bindings == null) return (null, error);

			var step = (goalStepIndex != -1 && goalStepIndex < goal.GoalSteps.Count) ? goal.GoalSteps[goalStepIndex] : null;
			var eventsToRun = bindings.Where(p => p.EventScope == EventScope.GoalError).ToList();

			return await HandleGoalError(goal, error, step, eventsToRun);

		}

		public async Task<(object? Variables, IBuilderError? Error)> RunBuildStepEvents(string eventType, Goal goal, GoalStep step, int stepIdx)
		{
			context.AddOrReplace(ReservedKeywords.Goal, goal);
			context.AddOrReplace(ReservedKeywords.Step, step);
			context.AddOrReplace(ReservedKeywords.StepIndex, stepIdx);

			var (vars, error) = await RunStepEvents(eventType, goal, step, true);
			if (error != null) return (vars, new BuilderError(error));
			return (vars, null);
		}


		public async Task<(object? Variables, IError? Error)> RunStepEvents(string eventType, Goal goal, GoalStep step, bool isBuilder = false)
		{
			var events = (isBuilder) ? await GetBuilderEvents() : await GetRuntimeEvents();
			if (events == null)
			{
				return (null, null);
			}

			var eventsToRun = events.Where(p => p.EventType == eventType && p.EventScope == EventScope.Step).ToList();
			for (var i = 0; i < eventsToRun.Count; i++)
			{
				var eve = eventsToRun[i];
				if (ActiveEvents.ContainsKey(eve.Id)) continue;
				if (GoalHasBinding(goal, eve) && IsStepMatch(step, eve))
				{
					var (variables, error) = await Run(eve, goal, step, isBuilder: isBuilder);
					if (error != null && error is not IErrorHandled) return (variables, error);
				}
			}
			return (null, null);
		}


		public async Task<(object? Variables, IError? Error)> RunOnErrorStepEvents(IError error, Goal goal, GoalStep step, bool isBuilder = false)
		{
			if (error is EndGoal)
			{
				return (null, error);
			}

			List<EventBinding>? bindings = (isBuilder) ? builderEvents : runtimeEvents;
			if (bindings == null) return (null, error);

			List<EventBinding> eventsToRun = new();
			eventsToRun.AddRange(bindings.Where(p => p.EventType == EventType.Before && p.EventScope == EventScope.StepError).ToList());

			var errorHandler = StepHelper.GetErrorHandlerForStep(step.ErrorHandlers, error);
			if (errorHandler != null)
			{
				if (errorHandler.GoalToCall != null)
				{
					var goalToBindTo = new GoalToBindTo(goal.RelativeGoalPath);

					var eventBinding = new EventBinding(EventType.Before, EventScope.StepError, goalToBindTo, errorHandler.GoalToCall,
						true, step.Number, step.Text, true, null, errorHandler.IgnoreError, errorHandler.Key, errorHandler.Message, errorHandler.StatusCode, IsLocal: true, IsOnStep: true);

					eventsToRun.Add(eventBinding);
				}
				else if (errorHandler.IgnoreError)
				{
					if (AppContext.TryGetSwitch(ReservedKeywords.DetailedError, out bool isDetailedError))
					{
						ShowLogError(goal, step, error);
					}
					return (null, new ErrorHandled(error));
				}
			}

			eventsToRun.AddRange(bindings.Where(p => p.EventType == EventType.After && p.EventScope == EventScope.StepError).ToList());

			if (eventsToRun.Count == 0)
			{
				// todo: strange
				if (goal.ParentGoal != null) return (null, error);
				return (null, error);
			}
			else
			{
				foreach (var eve in eventsToRun)
				{
					if (ActiveEvents.ContainsKey(eve.Id)) continue;
					if (GoalHasBinding(goal, eve) && IsStepMatch(step, eve) && EventMatchesError(eve, error))
					{
						var eventError = await Run(eve, goal, step, error);
						if (eventError.Error != null) return (eventError.Variables, new MultipleError(error).Add(eventError.Error));

						if (eve.OnErrorContinueNextStep) return (eventError.Variables, null);

						return (eventError.Variables, new ErrorHandled(error));
					}

				}
			}

			return (null, error);
		}


		public async Task<(object? Variables, IError? Error)> RunOnModuleError(MethodInfo method, IError error, Exception ex)
		{
			List<EventBinding>? bindings = runtimeEvents;
			if (bindings == null) return (null, error);

			List<EventBinding> eventsToRun = new();
			eventsToRun.AddRange(bindings.Where(p => p.EventScope == EventScope.ModuleError).ToList());

			if (eventsToRun.Count == 0)
			{
				return (null, error);
			}

			var goal = error.Goal;
			var step = error.Step;

			foreach (var eve in eventsToRun)
			{
				if (ActiveEvents.ContainsKey(eve.Id)) continue;
				if (EventMatchesError(eve, error))
				{
					var eventError = await Run(eve, goal, step, error);
					if (eventError.Error != null) return (eventError.Variables, new MultipleError(error).Add(eventError.Error));

					if (eve.OnErrorContinueNextStep) return (eventError.Variables, null);

					return (eventError.Variables, new ErrorHandled(error));
				}

			}


			return (null, error);
		}

		private async Task<(object? Variables, IError? Error)> Run(EventBinding eve, Goal? callingGoal = null, GoalStep? callingStep = null, IError? error = null, bool isBuilder = false)
		{

			try
			{
				eve.Stopwatch = Stopwatch.StartNew();
				if (error != null && AppContext.TryGetSwitch(ReservedKeywords.DetailedError, out bool isDetailedError))
				{
					ShowLogError(callingStep?.Goal, callingStep, error);
				}

				string goalName = eve.GoalToCall.ToString() ?? "";
				if (string.IsNullOrEmpty(goalName))
				{
					return (null, new RuntimeEventError("Goal name is empty", eve, callingGoal, callingStep));
				}

				eve.Goal = callingGoal;
				eve.GoalStep = callingStep;
				eve.Instruction = callingStep?.Instruction;

				eve.GoalToCall.Parameters.AddOrReplace(ReservedKeywords.Event, eve);
				eve.GoalToCall.Parameters.AddOrReplace(ReservedKeywords.IsEvent, true);
				eve.GoalToCall.Parameters.AddOrReplace(ReservedKeywords.Error, error);
			
				if (eve.IsOnStep) {
					eve.GoalToCall.Name = Path.Join(callingGoal?.RelativeGoalFolderPath ?? "", eve.GoalToCall.Name);
				} else if (!eve.GoalToCall.Name.StartsWith("/"))
				{
					eve.GoalToCall.Name = Path.Join("/events", eve.GoalToCall.Name);
				}

				logger.LogDebug("Run event type {0} on scope {1}, binding to {2} calling {3}", eve.EventType.ToString(), eve.EventScope.ToString(), eve.GoalToBindTo, eve.GoalToCall);

				ActiveEvents.TryAdd(eve.Id, eve.GoalToCall.Name);

				if (callingStep != null) caller.SetStep(callingStep);
				if (callingGoal != null) caller.SetGoal(callingGoal);

				Task<(object? Variables, IError? Error)> task;
				if (eve.GoalToCall.Name.StartsWith("apps/") || eve.GoalToCall.Name.StartsWith("/apps/"))
				{
					throw new Exception($"Callling app from event is not supported. {ErrorReporting.CreateIssueNotImplemented}");

				}
				else
				{
					task = caller.RunGoal(eve.GoalToCall, isolated: !eve.IsLocal);
				}

				if (eve.WaitForExecution)
				{
					try
					{
						await task;
					}
					catch { }
				}
				ActiveEvents.Remove(eve.Id, out _);


				if (task.Exception != null)
				{
					var exception = task.Exception.InnerException ?? task.Exception;
					if (isBuilder)
					{
						return (null, new BuilderEventError(exception.Message, eve, callingGoal, callingStep, Exception: exception));
					}
					return (null, new RuntimeEventError(exception.Message, eve, callingGoal, callingStep, Exception: exception));
				}

				var result = task.Result;


				object? Variables = null;
				if (result.Variables != null)
				{
					Variables = result.Variables;
					/*
					foreach (var parameter in result.Variables)
					{
						memoryStack.Put(parameter.Key, parameter.Value);
					}*/
				}
				else if (result.Error is Return r)
				{
					Variables = r.Variables;
				}

				if (result.Error is null or IErrorHandled or IUserDefinedError) return (Variables, result.Error);
				/*if (result.Error is IErrorHandled eh) return (result.Variables, eh);
				if (result.Error is IUserDefinedError ude)
				{
					return (null, (IEventError)ude);
				}*/

				if (isBuilder)
				{
					return (Variables, new BuilderEventError(result.Error.Message, eve, callingGoal, callingStep, InitialError: result.Error));
				}
				return (Variables, new RuntimeEventError(result.Error.Message, eve, callingGoal, callingStep, InitialError: result.Error));
			}
			catch (Exception ex)
			{
				throw;
			}
			finally
			{
				eve.Stopwatch.Stop();
			}


		}



		private bool EventMatchesError(EventBinding eve, IError error)
		{
			if (eve.ErrorKey != null && (eve.ErrorKey == "*" || eve.ErrorKey.Equals(error.Key, StringComparison.OrdinalIgnoreCase))) return true;
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
			if (!eventBinding.IsLocal && step.Goal.IsOS && !eventBinding.IncludeOsGoals) return false;
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
			if (!eventBinding.IsLocal)
			{
				if (goal.Visibility == Visibility.Private && !eventBinding.IncludePrivate || eventBinding.GoalToBindTo == null) return false;
				if (goal.IsOS && !eventBinding.IncludeOsGoals) return false;
			}

			if (goal.GoalName == eventBinding.GoalToCall.Name) return false;

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
				var result = goal.GoalFileName.ToLower() == goalToBindTo || goal.RelativeGoalPath.Equals(goalToBindTo.AdjustPathToOs(), StringComparison.OrdinalIgnoreCase);
				return result;
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
