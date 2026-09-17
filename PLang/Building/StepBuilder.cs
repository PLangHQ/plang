using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using PLang.Attributes;
using PLang.Building.Model;
using PLang.Building.Parsers;
using PLang.Errors;
using PLang.Errors.Builder;
using PLang.Errors.Handlers;
using PLang.Events;
using PLang.Events.Types;
using PLang.Exceptions;
using PLang.Exceptions.AskUser;
using PLang.Interfaces;
using PLang.Models;
using PLang.Runtime;
using PLang.Services.CompilerService;
using PLang.Services.LlmService;
using PLang.Utils;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using static PLang.Modules.BaseBuilder;

namespace PLang.Building;

public interface IStepBuilder
{
	Task<IBuilderError?> BuildStep(Goal goal, int stepNr, List<string>? excludeModules = null, IBuilderError? invalidFunctionError = null);

	// Asks the decider which module every step about to be built uses, in one request, before any
	// of them are built. Answers land in the cache and BuildStep reads them instead of asking.
	Task PrefetchModules(Goal goal, IReadOnlyList<int> stepIndexes);
}

public class StepBuilder : IStepBuilder
{
	private readonly IPLangFileSystem fileSystem;
	private readonly ILlmServiceFactory llmServiceFactory;
	private readonly Lazy<ILogger> logger;
	private readonly IInstructionBuilder instructionBuilder;
	private readonly IEventRuntime eventRuntime;
	private readonly ITypeHelper typeHelper;
	private readonly MemoryStack memoryStack;
	private readonly VariableHelper variableHelper;
	private readonly IErrorHandlerFactory exceptionHandlerFactory;
	private readonly PLangAppContext appContext;
	private readonly PLangContext context;
	private readonly ISettings settings;
	private readonly IEngine engine;
	private readonly PrParser prParser;
	private readonly IGoalParser goalParser;
	private readonly IBuilderDecider decider;
	private readonly IBuilderDeciderCache deciderCache;

	private const double DeciderConfidenceThreshold = BuilderDecider.ConfidenceThreshold;
	private IMemoryStackAccessor memoryStackAccessor;

	public StepBuilder(Lazy<ILogger> logger, IPLangFileSystem fileSystem, ILlmServiceFactory llmServiceFactory,
				IInstructionBuilder instructionBuilder, IEventRuntime eventRuntime, ITypeHelper typeHelper,
				IMemoryStackAccessor memoryStackAccessor, VariableHelper variableHelper, IErrorHandlerFactory exceptionHandlerFactory,
				PLangAppContext appContext, IPLangContextAccessor contextAccessor, ISettings settings, IEngine engine,
				PrParser prParser, IGoalParser goalParser, IBuilderDecider decider, IBuilderDeciderCache deciderCache)
	{
		this.decider = decider;
		this.deciderCache = deciderCache;
		this.fileSystem = fileSystem;
		this.llmServiceFactory = llmServiceFactory;
		this.logger = logger;
		this.instructionBuilder = instructionBuilder;
		this.eventRuntime = eventRuntime;
		this.typeHelper = typeHelper;
		this.memoryStack = memoryStackAccessor.Current;
		this.variableHelper = variableHelper;
		this.exceptionHandlerFactory = exceptionHandlerFactory;
		this.appContext = appContext;
		this.context = contextAccessor.Current;
		this.settings = settings;
		this.engine = engine;
		this.prParser = prParser;
		this.goalParser = goalParser;
		this.memoryStackAccessor = memoryStackAccessor;
	}

	public async Task<IBuilderError?> BuildStep(Goal goal, int stepIndex, List<string> excludeModules, IBuilderError? previousBuildError = null)
	{
		var step = (stepIndex < goal.GoalSteps.Count) ? goal.GoalSteps[stepIndex] : null;
		if (step == null)
		{
			return new GoalBuilderError($"Step nr. {stepIndex + 1} could not be loaded from goal {goal.GoalName}. This is unusual behaviour and should not happen. Try deleting the .pr file from {goal.AbsolutePrFolderPath}.", goal);
		}
		if (previousBuildError?.ErrorChain.Count > 3)
		{
			return new StepBuilderError($"Could not get answer from LLM. Will NOT try again. Tried {previousBuildError?.ErrorChain.Count} times. Will continue to build next step.", step);
		}

		try
		{
			// check if step has been build to speed up the build process
			var hasBeenBuild = await StepHasBeenBuild(step, stepIndex, excludeModules);
			if (hasBeenBuild.Error != null && hasBeenBuild.Error.Key != "InvalidInstructionFile") return hasBeenBuild.Error;
			if (hasBeenBuild.IsBuilt) return null;

			var (vars, error) = await eventRuntime.RunBuildStepEvents(EventType.Before, goal, step, stepIndex);
			if (error != null) return error;

			// build info about step, name, description and module type
			(step, error) = await BuildStepInformationWithRetry(goal, step, stepIndex, excludeModules, previousBuildError);
			if (error != null) return error;

			// builds the instruction set to execute
			(var instruction, error) = await BuildInstruction(this, goal, step);
			if (error != null) return error;

			// builds properties on the step, caching, errorhandling, logger
			(step, error) = await BuildStepProperties(goal, step, instruction);
			if (error != null) return error;

			if (step.Confidence != null && step.Confidence < DeciderConfidenceThreshold)
			{
				logger.Value.LogWarning($"{step.Confidence:0.00} confidence");
			}

			if (!string.IsNullOrEmpty(step.Inconsistency))
			{
				logger.Value.LogWarning($"  - ⚠️  Inconsistency: {step.Inconsistency}");
			}
			//CheckForBuildRunner(goal, step, instruction);

			//Set reload to false after Build Instruction
			step.Reload = false;
			step.Generated = DateTime.Now;
			step.RelativeGoalPath = goal.RelativeGoalPath;

			var result = await eventRuntime.RunBuildStepEvents(EventType.After, goal, step, stepIndex);
			return result.Error;
		}
		catch (Exception ex)
		{
			IBuilderError error;
			if (ex.InnerException is PLang.Errors.Handlers.AskUserError)
			{
				ex = ex.InnerException;
			}

			if (ex is PLang.Errors.Handlers.AskUserError mse)
			{
				return await HandleAskUser(mse, goal, stepIndex, excludeModules);

			}
			else
			{
				error = new ExceptionError(ex, Message: ex.Message, Step: step, Goal: goal);
			}
			(var isHandled, var handlerError) = await exceptionHandlerFactory.CreateHandler().Handle(error);
			if (isHandled)
			{
				return await BuildStep(goal, stepIndex, excludeModules);
			}
			else
			{
				if (handlerError == null || handlerError == error) return error;

				return ErrorHelper.GetMultipleBuildError(error, handlerError);
			}
		}

	}

	private async Task<(Model.Instruction? Instruction, IBuilderError? Error)> BuildInstruction(StepBuilder stepBuilder, Goal goal, GoalStep step, IBuilderError? previousBuilderError = null)
	{
		(var instruction, var error) = await instructionBuilder.BuildInstruction(this, goal, step, previousBuilderError);
		if (error == null) return (instruction, null);

		error.Step = step;
		error.Goal = goal;

		if (previousBuilderError != null) error.ErrorChain.Add(previousBuilderError);

		if (ShouldReturnError(step, error)) return (instruction, error);

		logger.Value.LogWarning("Error getting instruction, will try again. Error:" + error.Message);

		return await BuildInstruction(stepBuilder, goal, step, error);

	}

	private bool ShouldReturnError(GoalStep step, IBuilderError error)
	{
		var result = (error.ErrorChain.Count > 2 || !error.Retry || !error.ContinueBuild || error is IInvalidModuleError);
		if (result) return result;

		return error.RetryCount < GetErrorCount(step, error);
	}

	private async Task<IBuilderError?> HandleAskUser(AskUserError mse, Goal goal, int stepIndex, List<string> excludeModules)
	{
		try
		{
			Console.WriteLine(mse.Message);
			var line = Console.ReadLine();

			var error = await mse.InvokeCallback(line);
			if (error != null && error is PLang.Errors.AskUser.AskUserError aue)
			{
				error = await HandleAskUserError(aue);
			}
			if (error != null)
			{
				return new BuilderError(error);
			}

			return await BuildStep(goal, stepIndex, excludeModules);
		}
		catch (AskUserError ex)
		{

			return await HandleAskUser(ex, goal, stepIndex, excludeModules);
		}
	}

	private async Task<IBuilderError?> HandleAskUserError(Errors.AskUser.AskUserError aue)
	{
		var (answer, error) = await AskUser.GetAnswer(engine, context, aue.Message);
		if (error != null) return new BuilderError(error);

		(var isHandled, error) = await aue.InvokeCallback([answer]);
		if (error is AskUserError aueSecond)
		{
			return await HandleAskUserError(aue);
		}

		if (error is ExceptionError) return new BuilderError(error, false);
		if (error != null) return new BuilderError(error);
		return null;
	}

	private void CheckForBuildRunner(Goal goal, GoalStep step, Instruction instruction)
	{
		var program = typeHelper.GetRuntimeModules().FirstOrDefault(p => p.FullName == step.ModuleType + ".Program");
		if (program == null) return;

		var gf = instruction.Function as GenericFunction;
		if (gf == null) return;

		var methods = program.GetMethods().Where(p => p.Name == gf.Name);
		if (methods.Any()) return;
		/*
		var attribute = method.GetCustomAttribute(typeof(BuildRunner));
		if (attribute != null)
		{
			string goalFiles = "";
			//Engine.RunGoal(attribute.ToString(), goalFiles)
			int i = 0;
		}*/

	}

	private async Task<(bool IsBuilt, IBuilderError? Error)> StepHasBeenBuild(GoalStep step, int stepIndex, List<string> excludeModules)
	{
		AppContext.TryGetSwitch(ReservedKeywords.StrictBuild, out bool isStrict);
		if (isStrict && step.Number != stepIndex) return (false, null);
		if (step.PrFileName == null || excludeModules.Count > 0) return (false, null);
		if (!step.PrFileName.StartsWith((stepIndex + 1).ToString().PadLeft(2, '0'))) return (false, null);

		if (!fileSystem.File.Exists(step.AbsolutePrFilePath))
		{
			return (false, null);
		}

		(var instruction, var error) = InstructionCreator.Create(step.AbsolutePrFilePath, fileSystem);
		if (error != null) return (false, new BuilderError(error));

		if (instruction!.Function == null || string.IsNullOrEmpty(instruction.Function.Name)) return (false, null);

		bool doReload = (step.Reload || instruction.Reload || step.Text != instruction.Text);
		step.Reload = doReload;
		if (step.Reload) return (!step.Reload, null);

		var gf = instruction.Function;
		// lets load the return value into memoryStack
		if (gf.ReturnValues?.Count > 0)
		{
			await LoadVariablesIntoMemoryStack(gf, memoryStack, settings);
		}

		var builderRun = await this.instructionBuilder.RunStepValidation(step, instruction, gf);
		if (builderRun.Error != null) return (false, builderRun.Error);

		logger.Value.LogInformation($"{step.LineNumber}: Step {step.Name} is already built");
		return (true, null);
	}
	public Dictionary<string, List<IBuilderError>> ErrorCount { get; set; } = new();

	private async Task<(GoalStep, IBuilderError?)> BuildStepInformationWithRetry(Goal goal, GoalStep step, int stepIndex, List<string> excludeModules, IBuilderError? prevError = null)
	{
		if (step.ValidationErrors.Count > 0 && !string.IsNullOrEmpty(step.ModuleType))
		{
			logger.Value.LogInformation($"{step.LineNumber}: Using module {step.ModuleType} for {step.Text.Trim(['\n', '\r', '\t']).MaxLength(80)}");
			// since this is it contains validation error, no need to find out the module type
			// just go straight into fixing the method of the module
			return (step, null);
		}

		var result = await BuildStepInformation(goal, step, stepIndex, excludeModules, prevError);
		if (result.Error == null) return result;

		if (prevError != null) result.Error.ErrorChain.Add(prevError);
		if (ShouldReturnError(step, result.Error)) return (step, result.Error);

		logger.Value.LogWarning($"- Error building step, will try again. Error: {result.Error.Message}");

		return await BuildStepInformationWithRetry(goal, step, stepIndex, excludeModules, result.Error);
	}

	// One request for the whole goal instead of one per step. The state is every step of the goal,
	// including the ones that are not being rebuilt, because that context is what makes the answers
	// better: measured against asking step by step, the choices were identical on 37 of 37 steps
	// while 11 answers rose above the confidence threshold and 3 fell below it.
	public async Task PrefetchModules(Goal goal, IReadOnlyList<int> stepIndexes)
	{
		if ((AppContext.GetData("decider") as string) == "off" || goal.IsSystem) return;
		// Worth doing even for a single changed step. It is the same one request, and the state is
		// the whole goal, so an edited step is decided knowing the steps around it, which is where
		// the accuracy came from: 11 answers rose above the threshold on that context alone.
		if (stepIndexes.Count == 0) return;

		// A goal built a second time, after a retry or after a running app edited it, must not read
		// the previous build's answers.
		var cached = deciderCache.ForGoal(goal);
		cached.Clear();

		var modules = typeHelper.GetModulesDictionary(null);

		// Every question in this phase offers the same 48 modules, and the api has no way to name a
		// criteria set once and point at it, so each copy of a description is paid for again. The
		// descriptions go in the state, which is shared, and the options are bare names. Measured on
		// 3 goals and 37 steps: 294 KB down to 93 KB, the same module chosen 37 out of 37 times.
		// Dropping the descriptions altogether is a different thing and is not safe: it answers
		// confidently and wrongly, picking the ui module for template steps.
		var catalogue = new StringBuilder("\n\nThese are the plang modules you may choose from:\n");
		var options = new Dictionary<string, string>();
		foreach (var module in modules)
		{
			catalogue.AppendLine($"- {module.Key}: {module.Value}");
			options[module.Key] = null!;
		}

		var questions = new Dictionary<string, DeciderQuestion>();
		foreach (var index in stepIndexes)
		{
			var step = goal.GoalSteps[index];
			// A module the developer named is not a decision, so it is not worth asking about.
			if (GetUserRequestedModule(step).Count == 1) continue;
			questions[QuestionKey(step)] = new DeciderQuestion(
				$"Step {step.Index + 1} of this goal is `{step.Text.Trim()}`. Which plang module implements what step {step.Index + 1} does?",
				options);
		}
		if (questions.Count == 0) return;

		var (answers, error) = await decider.Choose(GoalState(goal) + catalogue, questions);
		if (error != null || answers == null)
		{
			// Nothing is lost: with an empty cache every step asks for itself, exactly as before.
			logger.Value.LogWarning($"Decider could not choose modules for {goal.GoalName} in one request, each step will ask on its own: {error?.Message}");
			return;
		}

		foreach (var index in stepIndexes)
		{
			var step = goal.GoalSteps[index];
			if (!answers.TryGetValue(QuestionKey(step), out var answer)) continue;
			cached.SetModule(step, new ModuleChoice(answer.Choice, answer.Confidence, answer.Probabilities));
		}
		logger.Value.LogDebug($"Decider chose modules for {cached.ModuleCount} steps of {goal.GoalName} in one request");

		await PrefetchMethods(goal, stepIndexes, cached);
	}

	// With the modules known, every step's method goes in a second request. Each question carries
	// its own module's method list, which is what makes one request possible at all: the options
	// are per question, only the state is shared.
	private async Task PrefetchMethods(Goal goal, IReadOnlyList<int> stepIndexes, GoalDeciderAnswers cached)
	{
		var questions = new Dictionary<string, DeciderQuestion>();
		var descriptions = new Dictionary<string, ClassDescription>();

		foreach (var index in stepIndexes)
		{
			var step = goal.GoalSteps[index];

			// The return variable is asked for every step, before anything about the module or the
			// method is known, because it needs neither. Steps whose module the llm still has to
			// pick were otherwise left out and each asked for itself afterwards, one request apiece.
			var variables = StepVariables(step);
			if (variables.Count > 0)
			{
				var targets = variables.ToDictionary(v => v, v => $"write the result into the variable {v}");
				targets["__none__"] = "the result is not written to a variable";
				questions[QuestionKey(step) + "__return__"] = new DeciderQuestion(
					$"Step {step.Index + 1} of this goal is `{step.Text.Trim()}`. Which variable does step {step.Index + 1} write its result into, if it captures the result at all? Choose none when the step does not keep the result in a variable.",
					targets);
			}

			var module = ModuleForStep(step, cached);
			if (module == null) continue;

			if (!descriptions.TryGetValue(module, out var classDescription))
			{
				var programType = typeHelper.GetRuntimeType(module);
				if (programType == null) continue;
				var (described, error) = new ClassDescriptionHelper().GetClassDescription(programType);
				if (error != null || described == null) continue;
				descriptions[module] = classDescription = described;
			}
			// One method means there is nothing to choose, same rule as NarrowToDecidedMethod.
			if (classDescription.Methods.Select(m => m.MethodName).Distinct().Count() <= 1) continue;

			questions[QuestionKey(step)] = new DeciderQuestion(
				$"Step {step.Index + 1} of this goal is `{step.Text.Trim()}`. It uses the module {module}. Which method of that module does step {step.Index + 1} call?",
				MethodCriteria(classDescription));
		}
		if (questions.Count == 0) return;

		var (answers, error2) = await decider.Choose(GoalState(goal), questions);
		if (error2 != null || answers == null)
		{
			logger.Value.LogWarning($"Decider could not choose methods for {goal.GoalName} in one request, each step will ask on its own: {error2?.Message}");
			return;
		}

		foreach (var index in stepIndexes)
		{
			var step = goal.GoalSteps[index];
			if (answers.TryGetValue(QuestionKey(step) + "__return__", out var returnAnswer))
			{
				cached.SetReturn(step, new ParameterChoice(returnAnswer.Choice, returnAnswer.Confidence, returnAnswer.Probabilities));
			}
			if (!answers.TryGetValue(QuestionKey(step), out var answer)) continue;
			cached.SetMethod(step, new MethodChoice(answer.Choice, answer.Confidence, answer.Probabilities));
		}
		logger.Value.LogDebug($"Decider chose methods and return variables for {cached.MethodCount} steps of {goal.GoalName} in one request");

		await PrefetchParameters(goal, stepIndexes, cached, descriptions);
	}

	// The last two requests. Each step's parameter questions are built by the same code the per step
	// path uses, then merged into one request with the step's index on the front of every key.
	// Returns go first and on their own, because a parameter may depend on whether the step captures
	// a result, and questions in one request cannot see each other's answers.
	private async Task PrefetchParameters(Goal goal, IReadOnlyList<int> stepIndexes, GoalDeciderAnswers cached,
		Dictionary<string, ClassDescription> descriptions)
	{
		var plans = new Dictionary<int, ParameterPlan>();
		foreach (var index in stepIndexes)
		{
			var step = goal.GoalSteps[index];
			var method = cached.Method(step);
			if (method == null || method.Confidence < DeciderConfidenceThreshold) continue;

			var module = ModuleForStep(step, cached);
			if (module == null || !descriptions.TryGetValue(module, out var classDescription)) continue;

			// DecideParameters only runs on a single method, so anything with overloads is left alone.
			var overloads = classDescription.Methods.Where(m => m.MethodName == method.Method).ToList();
			if (overloads.Count != 1) continue;

			var narrowed = new ClassDescription { Methods = overloads, Description = classDescription.Description };
			var plan = BuildParameterPlan(step, narrowed, variableHelper, memoryStack);
			if (plan.GiveUpReason != null) continue;
			if (plan.Questions.Count == 0 && !plan.HasReturn) continue;

			plans[index] = plan;
		}
		if (plans.Count == 0) return;

		var state = GoalState(goal);
		var answersByStep = plans.ToDictionary(p => p.Key, _ => new Dictionary<string, ParameterChoice>());

		// The returns were answered beside the methods, so they are already known here.
		foreach (var (index, plan) in plans)
		{
			if (!plan.HasReturn) continue;
			var choice = cached.Return(goal.GoalSteps[index]);
			if (choice != null) answersByStep[index]["__return__"] = choice;
		}

		// Then every parameter of every step, one request. The return outcome rides on each
		// question's own instructions, because the state is shared by all of them.
		var questions = new Dictionary<string, DeciderQuestion>();
		foreach (var (index, plan) in plans)
		{
			var step = goal.GoalSteps[index];
			answersByStep[index].TryGetValue("__return__", out var returnChoice);
			var returnFact = !plan.HasReturn ? null
				: returnChoice == null ? null
				: returnChoice.Choice == "__none__"
					? "This step does not write its result into any variable."
					: $"This step writes its result into the variable {returnChoice.Choice}.";

			// A variable the step writes into can still be one it reads from, so it stays a
			// candidate and is labelled rather than removed.
			if (returnChoice != null && returnChoice.Choice != "__none__") MarkReturnVariable(plan.Questions, returnChoice.Choice);

			foreach (var (name, question) in plan.Questions)
			{
				questions[$"{QuestionKey(step)}.{name}"] = Ask(step, plan.Method.MethodName, name, question, returnFact);
			}
		}
		if (questions.Count > 0)
		{
			var (answers, error) = await decider.Choose(state, questions);
			if (error != null || answers == null)
			{
				logger.Value.LogWarning($"Decider could not choose parameters for {goal.GoalName} in one request, each step will ask on its own: {error?.Message}");
				return;
			}
			foreach (var (index, plan) in plans)
			{
				var step = goal.GoalSteps[index];
				foreach (var name in plan.Questions.Keys)
				{
					if (!answers.TryGetValue($"{QuestionKey(step)}.{name}", out var answer)) continue;
					answersByStep[index][name] = new ParameterChoice(answer.Choice, answer.Confidence, answer.Probabilities);
				}
			}
		}

		foreach (var (index, answers) in answersByStep)
		{
			if (answers.Count == 0) continue;
			cached.SetParameters(goal.GoalSteps[index], answers);
		}
		logger.Value.LogDebug($"Decider chose parameters for {cached.ParameterStepCount} steps of {goal.GoalName} in one request");
	}

	// Same wording the per step path uses, with the step named so one request can hold many steps.
	private static DeciderQuestion Ask(GoalStep step, string method, string parameterName, ParameterQuestion question, string? returnFact)
	{
		// Two things this must not drop. The step number has to be the one GoalState lists the steps
		// by, or a question names a step the state does not contain. And the parameter's name has to
		// be in the text: without it `set %greeting% = "hello"` was asked twice for "a required
		// string" with no way to tell key from value, and the engine put the literal in key at 0.99.
		var instructions = $"Step {step.Index + 1} of this goal is `{step.Text.Trim()}`. It calls {method}."
			+ (returnFact == null ? "" : " " + returnFact)
			+ $" Parameter '{parameterName}': {question.Description}"
			+ $" Which of these is the value of '{parameterName}' in step {step.Index + 1}?";
		return new DeciderQuestion(instructions, question.Candidates);
	}

	// The module a step will end up on, when that is already known without asking the llm: either
	// the developer named it, or the decider just chose it with enough confidence.
	private string? ModuleForStep(GoalStep step, GoalDeciderAnswers cached)
	{
		var requested = GetUserRequestedModule(step);
		if (requested.Count == 1) return requested[0];

		var choice = cached.Module(step);
		if (choice == null || choice.Confidence < DeciderConfidenceThreshold) return null;
		return typeHelper.GetRuntimeType(choice.Module) == null ? null : choice.Module;
	}

	private List<string> StepVariables(GoalStep step)
	{
		return variableHelper.GetVariables(step.Text, memoryStack).Select(v => "%" + v.Path.Trim('%') + "%").Distinct().ToList();
	}

	private static string QuestionKey(GoalStep step) => "step" + step.Index;

	// Every step of the goal, so a question about one step is answered knowing the rest, even when
	// only a few steps are being rebuilt.
	private static string GoalState(Goal goal)
	{
		var text = new StringBuilder($"This is a plang goal called {goal.GoalName}. Its steps are numbered.\n\n");
		foreach (var step in goal.GoalSteps)
		{
			text.AppendLine($"step {step.Index + 1}: {step.Text.Trim()}");
		}
		return text.ToString();
	}

	private async Task<(GoalStep Step, IBuilderError? Error)> BuildStepInformation(Goal goal, GoalStep step, int stepIndex, List<string> excludeModules, IBuilderError? prevError = null)
	{
		// A module the user named in the step ([db], [ui]) is not a decision, it is an instruction.
		var userRequestedModule = GetUserRequestedModule(step);
		if (excludeModules != null)
		{
			foreach (var excludedModule in excludeModules) userRequestedModule.Remove(excludedModule);
		}
		if (userRequestedModule.Count == 1)
		{
			return (ApplyDerivedStep(goal, step, stepIndex, userRequestedModule[0], 1.0), null);
		}

		// First attempt goes to the decider. A retry (prevError set) means the decider's module did
		// not build, so the retry takes the llm path, which upgrades the model for exactly that case.
		var deciderSetting = AppContext.GetData("decider") as string;
		if (deciderSetting != "off" && prevError == null && !goal.IsSystem)
		{
			// The goal's modules are usually already decided, in one request, by PrefetchModules.
			// A miss is not a failure: the step asks for itself, which is what happened before.
			// Answers from an excludeModules retry are never cached, because prevError is set then.
			ModuleChoice? choice;
			IError? deciderError = null;
			choice = deciderCache.ForGoal(goal).Module(step);
			if (choice == null)
			{
				(choice, deciderError) = await decider.ChooseModule(step.Text, typeHelper.GetModulesDictionary(excludeModules));
			}
			if (deciderError != null)
			{
				logger.Value.LogWarning($"{step.LineNumber}: Decider failed, falling back to llm: {deciderError.Message}");
			}
			else if (choice != null && choice.Confidence >= DeciderConfidenceThreshold && typeHelper.GetRuntimeType(choice.Module) != null)
			{
				logger.Value.LogInformation($"{step.LineNumber}: Decider chose {choice.Module} ({choice.Confidence:0.00}) for {step.Text.Trim(['\n', '\r', '\t']).MaxLength(80)}");
				return (ApplyDerivedStep(goal, step, stepIndex, choice.Module, choice.Confidence), null);
			}
			else if (choice != null)
			{
				var contenders = string.Join(", ", choice.Probabilities.OrderByDescending(p => p.Value).Take(4).Select(p => $"{p.Key.Replace("PLang.Modules.", "")} {p.Value:0.00}"));
				logger.Value.LogInformation($"{step.LineNumber}: Decider confidence {choice.Confidence:0.00} for {choice.Module} is below {DeciderConfidenceThreshold}, falling back to llm. Contenders: {contenders}");
			}
		}

		LlmRequest llmQuestion = GetBuildStepInformationQuestion(goal, step, excludeModules, prevError);

		logger.Value.LogInformation($"{step.LineNumber}: Find module for {step.Text.Trim(['\n', '\r', '\t']).MaxLength(80)}");

		(var stepInformation, var llmError) = await llmServiceFactory.CreateHandler().Query<StepInformation>(llmQuestion);
		if (llmError != null) return (step, new BuilderError(llmError, false));
		if (stepInformation == null) return (step, new BuilderError("Didn't get any information"));

		if (stepInformation.Modules == null || stepInformation.Modules.Count == 0)
		{
			return (step, GetStepInformationError(step));
		}

		var module = stepInformation.Modules?.FirstOrDefault();
		if (module == null || module == "N/A")
		{
			return (step, GetStepInformationError(step));
		}
		var moduleType = typeHelper.GetRuntimeType(module);
		if (moduleType == null)
		{
			return (step, new InvalidModuleStepError(module, $"ModuleType {module} does not exist.", step, FixSuggestion: "Choose a module from list provided in <modules>"));
		}



		step.ModuleType = module;
		step.Name = stepInformation.StepName;
		step.Confidence = PLang.Utils.JsonConverters.ConfidenceConverter.Parse(stepInformation.Confidence);
		step.Inconsistency = stepInformation.Inconsistency;
		step.UserIntent = stepInformation.ExplainUserIntent;
		step.Description = stepInformation.StepDescription;
		step.PrFileName = GetPrFileName(stepIndex, step.Name);
		step.AbsolutePrFilePath = Path.Join(goal.AbsolutePrFolderPath, step.PrFileName);
		step.RelativePrPath = Path.Join(goal.RelativePrFolderPath, step.PrFileName);
		step.LlmRequest = llmQuestion;
		step.Number = stepIndex;
		step.RunOnce = GoalHelper.RunOnce(goal);
		return (step, null);

	}


	private int GetErrorCount(GoalStep step, IBuilderError error)
	{
		ErrorCount.TryGetValue(step.Text, out List<IBuilderError>? errors);
		if (errors == null) errors = new();

		errors.Add(error);
		ErrorCount!.AddOrReplace(step.Text, errors);
		return errors.Count;

	}

	private StepBuilderError GetStepInformationError(GoalStep step)
	{
		ErrorCount.TryGetValue(step.Text, out var errors);
		string errorCount = "";
		if (errors != null && errors.Count > 0)
		{
			errorCount = $"I tried {errorCount} times.";
		}

		string noBuildErrorMessage = $@"Could not find module for {step.Text}. {errorCount}";
		string fixSuggestions = $@"
Try defining the step in more detail.

You have 3 options:
	- Rewrite your step to fit better with a modules that you have installed. 
		How to write the step? Get help here https://github.com/PLangHQ/plang/blob/main/Documentation/modules/README.md
	- Install an App from that can handle your request and call that
	- Build your own module. This requires a C# developer knowledge

Builder will continue on other steps but not this one: ({step.Text}).
";
		return new StepBuilderError(noBuildErrorMessage, step, HelpfulLinks: "https://github.com/PLangHQ/plang/blob/main/Documentation/modules/README.md", Retry: false, FixSuggestion: fixSuggestions);
	}

	private string GetPrFileName(int stepIndex, string stepName)
	{
		var strStepNr = (stepIndex + 1).ToString().PadLeft(2, '0');
		return strStepNr + ". " + stepName + ".pr";
	}

	// The decider only answers "which module". The name, description and intent the llm used to
	// write are derived instead: the name from the step text, the description is the step, and the
	// intent is left empty so the method builder does not present the raw step as disambiguated.
	private GoalStep ApplyDerivedStep(Goal goal, GoalStep step, int stepIndex, string module, double confidence)
	{
		step.ModuleType = module;
		step.Confidence = confidence;
		step.Inconsistency = null;
		step.UserIntent = null;
		step.Name = DeriveStepName(step.Text);
		step.Description = step.Text.Trim();
		step.PrFileName = GetPrFileName(stepIndex, step.Name);
		step.AbsolutePrFilePath = Path.Join(goal.AbsolutePrFolderPath, step.PrFileName);
		step.RelativePrPath = Path.Join(goal.RelativePrFolderPath, step.PrFileName);
		step.LlmRequest = null;
		step.Number = stepIndex;
		step.RunOnce = GoalHelper.RunOnce(goal);
		return step;
	}

	// The step number prefix already makes the .pr file name unique, so the name only needs to be
	// readable and safe on every file system: ascii words from the step, a handful of them.
	private static string DeriveStepName(string text)
	{
		var cleaned = System.Text.RegularExpressions.Regex.Replace(text ?? "", "[^a-zA-Z0-9]+", " ").Trim().ToLowerInvariant();
		var name = string.Join("_", cleaned.Split(' ', StringSplitOptions.RemoveEmptyEntries).Take(6));
		if (name.Length > 40) name = name.Substring(0, 40).TrimEnd('_');
		return string.IsNullOrEmpty(name) ? "step" : name;
	}




	private async Task<(GoalStep step, IBuilderError? error)> BuildStepProperties(Goal goal, GoalStep step, Instruction instruction)
	{
		LlmRequest llmQuestion = await GetBuildStepPropertiesQuestion(goal, step, instruction);

		logger.Value.LogInformation($"  - Building properties for {step.Text.Trim(['\n', '\r', '\t']).MaxLength(80)}");

		(var stepProperties, var llmError) = await llmServiceFactory.CreateHandler().Query<StepProperties>(llmQuestion);
		if (llmError != null) return (step, new StepBuilderError(llmError, step));

		if (stepProperties == null) return (step, new StepBuilderError($"Could not get answer from LLM.", step));
		(stepProperties, var error) = ValidateGoalPaths(stepProperties, step);
		if (error != null) return (step, error);

		(bool canBeCached, bool canHaveErrorHandling, bool canBeAsync) = GetMethodSettings(step, instruction);
		step.ErrorHandlers = (canHaveErrorHandling) ? stepProperties.ErrorHandlers : null;
		step.WaitForExecution = (canBeAsync) ? stepProperties.WaitForExecution : true;
		step.LoggerLevel = GetLoggerLevel(stepProperties.LoggerLevel);
		// cannot put caching on caching module
		step.CacheHandler = (canBeCached) ? stepProperties.CachingHandler : null;

		return (step, null);
	}

	private (StepProperties, IBuilderError?) ValidateGoalPaths(StepProperties stepProperties, GoalStep step)
	{
		for (int i =0;i<stepProperties.ErrorHandlers?.Count;i++)
		{
			var errorHandler = stepProperties.ErrorHandlers[i];
			
			if (errorHandler.GoalToCall == null) continue;	

			(var goalFound, var error) = GoalHelper.GetGoalPath(step, errorHandler.GoalToCall, goalParser.GetGoals(), prParser.GetSystemGoals());
			if (error != null) return (stepProperties, new BuilderError(error) {  Retry = false });

			if (goalFound != null)
			{
				errorHandler.GoalToCall.Path = goalFound.RelativePrPath;
				stepProperties.ErrorHandlers[i] = errorHandler;
			}
		}
		return (stepProperties, null);
	}

	private string? GetLoggerLevel(string? loggerLevel)
	{
		if (loggerLevel == null) return null;

		loggerLevel = loggerLevel.ToLower();
		if (loggerLevel == "error" || loggerLevel == "warning" || loggerLevel == "information" || loggerLevel == "debug" || loggerLevel == "trace") return loggerLevel;
		return null;
	}

	private async Task<LlmRequest> GetBuildStepPropertiesQuestion(Goal goal, GoalStep step, Instruction instruction)
	{

		(bool canBeCached, bool canHaveErrorHandling, bool canBeAsync) = GetMethodSettings(step, instruction);

		var stepInformationSystemPath = fileSystem.Path.Join(fileSystem.SystemDirectory, "modules", "StepPropertiesSystem.llm");
		if (!fileSystem.File.Exists(stepInformationSystemPath))
		{
			throw new Exception($"StepPropertiesSystem.llm is missing from system. It should be located at {stepInformationSystemPath}");
		}
		var content = fileSystem.File.ReadAllText(stepInformationSystemPath);

		var templateProgram = engine.GetProgram<Modules.TemplateEngineModule.Program>();

		Dictionary<string, object> variables = new();
		variables.Add("canBeCached", canBeCached);
		variables.Add("canHaveErrorHandling", canHaveErrorHandling);
		variables.Add("canBeAsync", canBeAsync);

		var obj = new { Name = instruction.Function.Name, Parameters = instruction.Function.Parameters, ReturnValue = instruction.Function.ReturnValues };
		variables.Add("function", obj);

		(var system, var error) = await templateProgram.RenderContent(content, stepInformationSystemPath, variables);


		var stepPropertiesScheme = TypeHelper.GetJsonSchema(typeof(StepProperties));

		List<LlmMessage> promptMessage = new();
		promptMessage.Add(new LlmMessage("system", system));
		promptMessage.Add(new LlmMessage("user", step.Text));

		var llmRequest = new LlmRequest("StepPropertiesBuilder", promptMessage);
		llmRequest.Step = step;
		llmRequest.Goal = goal;
		llmRequest.scheme = stepPropertiesScheme;

		if (step.PrFileName == null) llmRequest.Reload = true;
		return llmRequest;


	}

	private (bool, bool, bool) GetMethodSettings(GoalStep step, Instruction instruction)
	{
		bool canBeCached = true;
		bool canHaveErrorHandling = true;
		bool canBeAsync = true;

		var moduleType = typeHelper.GetRuntimeType(step.ModuleType);
		var gf = instruction.Function as GenericFunction;
		if (moduleType == null || gf == null)
		{
			return (canBeCached, canHaveErrorHandling, canBeAsync);
		}

		var method = moduleType.GetMethods(BindingFlags.Public | BindingFlags.Instance).FirstOrDefault(p => p.Name == gf.Name);
		if (method == null)
		{
			return (canBeCached, canHaveErrorHandling, canBeAsync);
		}

		var attribute = method.GetCustomAttribute<MethodSettingsAttribute>();
		if (attribute != null)
		{
			canBeCached = attribute.CanBeCached;
			canHaveErrorHandling = attribute.CanHaveErrorHandling;
			canBeAsync = attribute.CanBeAsync;
		}

		return (canBeCached, canHaveErrorHandling, canBeAsync);
	}



	private LlmRequest GetBuildStepInformationQuestion(Goal goal, GoalStep step, List<string> excludeModules, IBuilderError? prevError = null)
	{
		// user might define in his step specific module.

		var modulesAvailable = typeHelper.GetModulesAsString(excludeModules);
		
		var userRequestedModule = GetUserRequestedModule(step);
		if (excludeModules != null)
		{
			if (excludeModules.Count == 1 && userRequestedModule.Count == 1 && userRequestedModule.FirstOrDefault(p => p.Equals(excludeModules[0])) != null)
			{
				throw new BuilderStepException($"Could not map {step.Text} to {userRequestedModule[0]}");
			}
			foreach (var excludedModule in excludeModules)
			{
				userRequestedModule.Remove(excludedModule);
			}
		}

		if (userRequestedModule.Count > 0)
		{
			modulesAvailable = string.Join(", ", userRequestedModule);
		}
		var jsonScheme = TypeHelper.GetJsonSchema(typeof(StepInformation));

		var stepInformationSystemPath = fileSystem.Path.Join(fileSystem.SystemDirectory, "modules", "StepInformationSystem.llm");
		if (!fileSystem.File.Exists(stepInformationSystemPath))
		{
			throw new Exception($"StepInformationSystem.llm is missing from system. It should be located at {stepInformationSystemPath}");
		}
		var system = fileSystem.File.ReadAllText(stepInformationSystemPath);
		
		string assistant = $@"This is a list of modules you can choose from
<modules>
{modulesAvailable}
<modules>
";
		var variablesInStep = variableHelper.GetVariables(step.Text, memoryStack);
		if (variablesInStep.Count > 0)
		{
			assistant += $@"
<variables>
{string.Join(",", variablesInStep.Select(p => p.Path + $"({p.Type})"))}
<variables>
";
		}


		List<LlmMessage> promptMessage = new();
		promptMessage.Add(new LlmMessage("system", system));
		promptMessage.Add(new LlmMessage("assistant", assistant));
		promptMessage.Add(new LlmMessage("user", step.Text));

		if (prevError != null)
		{
			promptMessage.Add(new LlmMessage("assistant", ErrorHelper.MakeForLlm(prevError)));
		}

		var llmRequest = new LlmRequest("StepInformationBuilder", promptMessage);
		llmRequest.Step = step;
		llmRequest.Goal = goal;
		llmRequest.scheme = jsonScheme;
		if (prevError != null)
		{
			// upgrade model because of error
			llmRequest.model = "gpt-4o";
			llmRequest.Reload = true;
		}

		if (step.PrFileName == null || (excludeModules != null && excludeModules.Count > 0)) llmRequest.Reload = true;
		return llmRequest;
	}

	public async Task<IBuilderError?> LoadVariablesIntoMemoryStack(IGenericFunction gf, MemoryStack memoryStack, ISettings settings)
	{
		if (gf.ReturnValues != null && gf.ReturnValues.Count > 0)
		{
			foreach (var returnValue in gf.ReturnValues)
			{
				memoryStack.PutForBuilder(returnValue.VariableName, returnValue.Type);
			}
		}

		return await LoadParameters(gf, memoryStack, settings);
	}
	private async Task<IBuilderError?> LoadParameters(IGenericFunction gf, MemoryStack memoryStack, ISettings settings)
	{
		// todo: hack for now, should be able to load dynamically variables that are being set at build time
		// might have to structure the build
		if (gf == null || gf.Parameters == null || gf.Parameters.Count == 0) return null;

		foreach (var parameter in gf.Parameters)
		{
			if (VariableHelper.IsVariable(parameter.Value))
			{
				memoryStack.PutForBuilder(parameter.Name, parameter.Type);
			}
		}

		return null;
	}

	protected string GetVariablesInStep(GoalStep step)
	{
		var variables = variableHelper.GetVariables(step.Text, memoryStack);
		string vars = "";
		foreach (var variable in variables)
		{
			if (variable.Initiated)
			{
				vars += variable.Name + "(" + variable.Value + "), ";
			}
		}
		return vars;
	}

	private List<string> GetUserRequestedModule(GoalStep step)
	{
		var modules = typeHelper.GetRuntimeModules();
		List<string> forceModuleType = new List<string>();
		var match = Regex.Match(step.Text.Trim(), @"^\[[\w]+\]", RegexOptions.IgnoreCase | RegexOptions.Multiline);
		if (match.Success)
		{
			var matchValue = match.Value.ToLower().Replace("[", "").Replace("]", "");
			List<string> userRequestedModules = new List<string>();
			var module = modules.FirstOrDefault(p => p.FullName.Equals(matchValue, StringComparison.OrdinalIgnoreCase));
			if (module != null)
			{
				userRequestedModules.Add(module.FullName);
			}
			else
			{
				foreach (var tmp in modules)
				{
					if (tmp.FullName != null && tmp.FullName.Replace("PLang.Modules.", "").ToLower().Contains(matchValue.ToLower()))
					{
						userRequestedModules.Add(tmp.FullName.Replace(".Program", ""));
					}
				}
			}
			if (userRequestedModules.Count == 1)
			{
				forceModuleType.Add(userRequestedModules[0]);
			}
			else if (userRequestedModules.Count > 1)
			{
				forceModuleType = userRequestedModules;
			}
		}
		return forceModuleType;
	}
}

