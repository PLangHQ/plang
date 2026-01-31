using LightInject;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using OpenAI.Audio;
using PLang.Building.Model;
using PLang.Building.Parsers;
using PLang.Container;
using PLang.Errors;
using PLang.Errors.Builder;
using PLang.Events;
using PLang.Events.Types;
using PLang.Exceptions;
using PLang.Interfaces;
using PLang.Models;
using PLang.Modules.DbModule;
using PLang.Runtime;
using PLang.Services.LlmService;
using PLang.Utils;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reactive.Concurrency;
using System.Reflection;
using System.Reflection.Emit;
using System.Text.RegularExpressions;
using static PLang.Modules.BaseBuilder;
using static PLang.Modules.DbModule.ModuleSettings;

namespace PLang.Building
{

	public interface IGoalBuilder
	{
		Task<IBuilderError?> BuildGoal(IServiceContainer container, Goal goal, PLangContext context, int errorCount = 0, int goalIndex = 0);
		void AddToBuildErrors(IBuilderError buildStepError);

		public List<IBuilderError> BuildErrors { get; init; }
	}


	public class GoalBuilder : IGoalBuilder
	{
		private readonly IPLangFileSystem fileSystem;
		private readonly ILlmServiceFactory llmServiceFactory;
		private readonly ILogger logger;
		private readonly IGoalParser goalParser;
		private readonly IEventRuntime eventRuntime;
		private readonly ITypeHelper typeHelper;
		private readonly IPrParser prParser;
		private readonly ISettings settings;
		private readonly ModuleSettings dbSettings;
		private readonly IInstructionBuilder instructionBuilder;
		private readonly VariableHelper variableHelper;
		private readonly MethodHelper methodHelper;
		private readonly IStepBuilder stepBuilder;
		public List<IBuilderError> BuildErrors { get; init; }
		public GoalBuilder(ILogger logger, IPLangFileSystem fileSystem, ILlmServiceFactory llmServiceFactory,
				IGoalParser goalParser, IStepBuilder stepBuilder, IEventRuntime eventRuntime, ITypeHelper typeHelper,
				IPrParser prParser, ISettings settings, Modules.DbModule.ModuleSettings dbSettings,
				IInstructionBuilder instructionBuilder, VariableHelper variableHelper, MethodHelper methodHelper)
		{

			this.fileSystem = fileSystem;
			this.llmServiceFactory = llmServiceFactory;
			this.logger = logger;
			this.goalParser = goalParser;
			this.stepBuilder = stepBuilder;
			this.eventRuntime = eventRuntime;
			this.typeHelper = typeHelper;
			this.prParser = prParser;
			this.settings = settings;
			this.dbSettings = dbSettings;
			this.instructionBuilder = instructionBuilder;
			this.variableHelper = variableHelper;
			this.methodHelper = methodHelper;
			BuildErrors = new();
		}



		public async Task<IBuilderError?> BuildGoal(IServiceContainer container, Goal goal, PLangContext context, int errorCount = 0, int goalIndex = 0)
		{
			Stopwatch stopwatch = Stopwatch.StartNew();
			GroupedBuildErrors groupedBuildErrors = new();

			logger.LogDebug($" - Goal has not change - validating steps {goal.GoalName} - {stopwatch.ElapsedMilliseconds}");
			var validationError = await ValidateSteps(goal);
			logger.LogDebug($" - Done validating steps {goal.GoalName} - {stopwatch.ElapsedMilliseconds}");

			var isBuiltResult = await GoalIsBuilt(goal, validationError, container.GetInstance<IEngine>(), context);
			if (isBuiltResult.Error != null) return isBuiltResult.Error;
			if (isBuiltResult.IsBuilt)
			{
				return null;
			}

			logger.LogInformation($"\nStart to build {goal.GoalName} - {goal.RelativeGoalPath}:{goal.GoalSteps.FirstOrDefault()?.LineNumber}");

			// Generate description and other properties for goal			
			(goal, var error) = await LoadMethodAndDescription(goal);
			if (error != null)
			{
				var result = await eventRuntime.RunGoalErrorEvents(goal, 0, error, true);
				if (result.Error != null) return new GoalBuilderError(result.Error, goal, ContinueBuild: false);

				return await BuildGoal(container, goal, context, errorCount, goalIndex);
			}
			logger.LogDebug($" - Run BuildGoal events {goal.GoalName} - {stopwatch.ElapsedMilliseconds}");
			var (vars, buildEventError) = await eventRuntime.RunBuildGoalEvents(EventType.Before, goal);
			if (!ContinueBuildGoal(buildEventError, goal))
			{
				return buildEventError;
			}

			for (int i = 0; i < goal.GoalSteps.Count; i++)
			{
				if (!goal.GoalSteps[i].HasChanged && goal.GoalSteps[i].IsValid) continue;

				logger.LogDebug($" - Building step {goal.GoalSteps[i].Text.MaxLength(20)} - {stopwatch.ElapsedMilliseconds}");
				var buildStepError = await BuildStep(goal, i);
				logger.LogDebug($" - Done building step {goal.GoalSteps[i].Text.MaxLength(20)} - {stopwatch.ElapsedMilliseconds}");
				if (buildStepError != null)
				{
					if (!buildStepError.ContinueBuild) return buildStepError;
					groupedBuildErrors.Add(buildStepError);
				}

			}

			logger.LogDebug($" - Cleanup and injections - {stopwatch.ElapsedMilliseconds}");
			RemoveUnusedPrFiles(goal);
			RegisterForPLangUserInjections(container, goal);

			logger.LogDebug($" - Done with cleanup, running build events - {stopwatch.ElapsedMilliseconds}");

			(vars, buildEventError) = await eventRuntime.RunBuildGoalEvents(EventType.After, goal);
			if (!ContinueBuildGoal(buildEventError, goal))
			{
				return buildEventError;
			}

			WriteToGoalPrFile(goal);
			logger.LogInformation($"Done building all goals {goal.GoalName} - It took {stopwatch.ElapsedMilliseconds}ms");

			return (groupedBuildErrors.Count > 0) ? groupedBuildErrors : null;
		}

		private async Task<(bool IsBuilt, IBuilderError? Error)> GoalIsBuilt(Goal goal, GroupedBuildErrors? validationError, IEngine engine, PLangContext context)
		{
			if (validationError == null) return (!goal.HasChanged, null);

			var missingSettings = validationError.ErrorChain.Where(p => p.Exception?.GetType() == typeof(MissingSettingsException));
			if (!missingSettings.Any())
			{
				if (validationError != null)
				{
					validationError.Step.IsValid = false;
					return (false, null);
				}
				return (goal.HasChanged, null);
			}

			var error = await MissingSettingsHelper.Handle(engine, context, missingSettings);
			if (error != null)
			{
				var me = new MultipleBuildError(validationError);
				me.Add(error);
				return (false, me);
			}

			return (!goal.HasChanged, null);

		}

		private async Task<IBuilderError?> BuildStep(Goal goal, int i, List<string>? excludeModules = null)
		{
			if (excludeModules == null) excludeModules = new();

			var buildStepError = await stepBuilder.BuildStep(goal, i, excludeModules);
			if (buildStepError != null)
			{
				buildStepError = await ContinueBuildStep(buildStepError, goal, i, excludeModules);
				if (buildStepError != null)
				{
					return buildStepError;
				}
			}
			goal.GoalSteps[i].Hash = null;
			goal.GoalSteps[i].Hash = JsonConvert.SerializeObject(goal.GoalSteps[i], GoalSerializer.Settings).ComputeHash().Hash;
			WriteToGoalPrFile(goal);

			return null;
		}

		private bool ContinueBuildGoal(IBuilderError? buildStepError, Goal goal)
		{
			if (buildStepError == null) return true;

			if (buildStepError.Goal == null) buildStepError.Goal = goal;

			AddToBuildErrors(buildStepError);
			

			return buildStepError.ContinueBuild;
		}

		public void AddToBuildErrors(IBuilderError buildStepError)
		{
			if (BuildErrors.FirstOrDefault(p => p == buildStepError) == null && 
				(BuildErrors.FirstOrDefault(p => p.Step?.AbsolutePrFilePath != buildStepError.Step?.AbsolutePrFilePath) == null && BuildErrors.FirstOrDefault(p => p.Step?.LineNumber == buildStepError.Step?.LineNumber) == null))
			{
				BuildErrors.Add(buildStepError);

				logger.LogWarning($"  - ❌ Error building goal - {buildStepError.MessageOrDetail}");
			}
		}

		private async Task<IBuilderError?> ContinueBuildStep(IBuilderError? buildStepError, Goal goal, int stepIndex, List<string> excludeModules)
		{
			if (buildStepError == null) return null;

			if (buildStepError.Goal == null) buildStepError.Goal = goal;
			if (buildStepError.Step == null) buildStepError.Step = goal.GoalSteps[stepIndex];

			if (buildStepError is IInvalidModuleError ime && excludeModules.Count < 3)
			{
				logger.LogWarning($"  - ❌ Error building step - Error Message: {buildStepError.MessageOrDetail}");

				excludeModules.Add(ime.ModuleType);
				logger.LogWarning($"  - 🔍 Will try to find another module - attempt {excludeModules.Count + 1} of 3");

				var result = await BuildStep(goal, stepIndex, excludeModules);
				return result;
			}
			else if (excludeModules.Count > 0)
			{
				logger.LogError($"  - ❌ Error finding module for step. I tried '{string.Join("', '", excludeModules)}' - Error message: {buildStepError.MessageOrDetail}");
			}
			AddToBuildErrors(buildStepError);			

			return buildStepError;
		}

		private async Task<GroupedBuildErrors?> ValidateSteps(Goal goal)
		{
			// Since step has not been build we disable build validation for all
			// steps that come after as they can be affected by previous steps. 
			// example of that, steps that creates table but is not build,
			// step after that insert into table will fail
			if (goal.HasChanged && !goal.IsSetup) return null;

			Stopwatch stopwatch = Stopwatch.StartNew();
			GroupedBuildErrors errors = new();
			bool stepsNotBuilt = false;
			foreach (var step in goal.GoalSteps)
			{
				logger.LogDebug($"   - Validating step {step.Text.MaxLength(10)} - {stopwatch.ElapsedMilliseconds}");
				var functionResult = step.GetFunction(fileSystem);
				if (functionResult.Function == null)
				{
					stepsNotBuilt = true;
					step.IsValid = false;
					continue;
				}

				if (functionResult.Error != null)
				{
					step.ValidationErrors.Add(functionResult.Error);
					step.IsValid = false;
					step.Reload = true;
					errors.Add(functionResult.Error);

					// skip rest of validation, we already know this step has invalid build
					continue;
				}


				logger.LogDebug($"   - Found function, validating... - {stopwatch.ElapsedMilliseconds}");

				(var parameterProperties, var returnObjectsProperties, var invalidFunctionError) = methodHelper.ValidateFunctions(step, null);
				if (invalidFunctionError != null)
				{
					step.ValidationErrors.Add(invalidFunctionError);
					step.Reload = true;
					step.IsValid = false;
					errors.Add(invalidFunctionError);

					// skip rest of validation, we already know this step has invalid build
					continue;
				}

				// all steps need to be build before running step validation
				if (!stepsNotBuilt)
				{
					logger.LogDebug($"   - Validated function, run builder methods - {stopwatch.ElapsedMilliseconds}");

					var builderRun = await instructionBuilder.RunStepValidation(step, step.Instruction, functionResult.Function);
					if (builderRun.Error != null)
					{
						builderRun.Error.Step = step;
						builderRun.Error.Goal = goal;

						step.ValidationErrors.Add(builderRun.Error);
						errors.Add(builderRun.Error);
						step.Reload = true;
						step.IsValid = false;

						// skip rest of validation, we already know this step has invalid build
						continue;
					}

					logger.LogDebug($"   - Done running builder methods - {stopwatch.ElapsedMilliseconds}");
				}

				if (string.IsNullOrEmpty(step.Hash))
				{
					step.IsValid = false;
					step.Reload = false;
					continue;
				}
				step.IsValid = true;

			}

			goal.IsValid = !goal.GoalSteps.Any(p => !p.IsValid);
			

			return (errors.Count > 0) ? errors : null;
		}


		private void RegisterForPLangUserInjections(IServiceContainer container, Goal goal)
		{
			foreach (var injection in goal.Injections)
			{

				RegisterForPLangUserInjections(container, injection.Type, injection.Path, injection.IsGlobal, injection.EnvironmentVariable, injection.EnvironmentVariableValue);

			}
		}

		private IBuilderError LoadInjections(Goal goal)
		{
			goal.Injections.RemoveAll(p => !p.AtSignInjection);

			var injectionSteps = goal.GoalSteps.Where(p => p.ModuleType == "PLang.Modules.InjectModule");
			foreach (var injection in injectionSteps)
			{
				var instruction = prParser.ParseInstructionFile(injection);
				if (instruction == null) continue;

				var gf = instruction.Function;

				var typeParam = gf.Parameters.FirstOrDefault(p => p.Name == "type");
				var pathToDllParam = gf.Parameters.FirstOrDefault(p => p.Name == "pathToDll");
				var isGlobalParam = gf.Parameters.FirstOrDefault(p => p.Name == "isDefaultOrGlobalForWholeApp");
				var environmentVariableParam = gf.Parameters.FirstOrDefault(p => p.Name == "environmentVariable");
				var environmentVariableValueParam = gf.Parameters.FirstOrDefault(p => p.Name == "environmentVariableValue");

				string type = (typeParam == null) ? null : (string)typeParam.Value;
				string pathToDll = (pathToDllParam == null) ? null : (string)pathToDllParam.Value;
				bool isGlobal = (isGlobalParam == null) ? false : (bool)isGlobalParam.Value;
				string? environmentVariable = (environmentVariableParam == null) ? null : (string?)environmentVariableParam.Value;
				string environmentVariableValue = (environmentVariableValueParam == null) ? null : (string?)environmentVariableValueParam.Value;

				var dependancyInjection = new Injections(type, pathToDll, isGlobal, environmentVariable, environmentVariableValue);

				goal.Injections.Add(dependancyInjection);


			}
			return null;


		}

		private void RegisterForPLangUserInjections(IServiceContainer container, string type, string path, bool isGlobal, string? environmentVariable = null, string? environmentVariableValue = null)
		{
			container.RegisterForPLangUserInjections(type, path, isGlobal, environmentVariable, environmentVariableValue);
		}

		private IBuilderError? WriteToGoalPrFile(Goal goal)
		{
			if (!fileSystem.Directory.Exists(goal.AbsolutePrFolderPath))
			{
				fileSystem.Directory.CreateDirectory(goal.AbsolutePrFolderPath);
			}

			var error = LoadInjections(goal);
			if (error != null) return error;

			goal.BuilderVersion = PlangHelper.GetVersion();
			goal.Hash = "";

			var json = JsonConvert.SerializeObject(goal, GoalSerializer.Settings);
			goal.Hash = json.ComputeHash().Hash;

			json = JsonConvert.SerializeObject(goal, GoalSerializer.Settings);
			fileSystem.File.WriteAllText(goal.AbsolutePrFilePath, json);
			return null;
		}

		private async Task<(Goal, IBuilderError?)> LoadMethodAndDescription(Goal goal)
		{
			Goal? oldGoal = JsonHelper.ParseFilePath<Goal>(fileSystem, goal.AbsolutePrFilePath);
			if (oldGoal != null)
			{
				goal.GoalInfo = oldGoal.GoalInfo;
			}


			return await CreateDescriptionForGoal(goal, oldGoal);

		}

		public record GoalDescription(string Description, Dictionary<string, string>? IncomingVariablesRequired = null);

		private async Task<(Goal, IBuilderError?)> CreateDescriptionForGoal(Goal goal, Goal? oldGoal)
		{

			if (!string.IsNullOrEmpty(goal.Description) && goal.GetGoalAsString() == oldGoal?.GetGoalAsString())
			{
				return (goal, null);
			}

			logger.LogDebug($" - Create desription for goal {goal.GoalName}");

			var promptMessage = new List<LlmMessage>();
			promptMessage.Add(new LlmMessage("system", $@"
You will receive a code written in Plang programming language
Your job is to write a description for this Goal called {goal.GoalName} and find out what variables are needed for the execution of the code.

Use the comments and steps to build the description,
Goal works like a function for programming language Plang. 
Goal defines 1 or more steps. 
Comments start with /
Steps start with dash(-). 
%Variable% is defined with starting and ending %.
When writing %variable% in description, escape the variable with \, e.g. \%variable\%
Analyze the goal and list out variables that are required to be sent to this goal to make it work, key is name of variable, value is description and write to IncomingVariablesRequired
Step that writes into a variable are creating that variable
Describe conditions that are affected by variables, describe what value of variable should be to call goal or perform some steps
[llm] or step starting with system: and contains scheme:, will create variables from that scheme automatically, e.g. scheme: {{ name:string }} will create %name% variable
Be concise
"));
			Type responseType = typeof(GoalDescription);
			string model = "gpt-4o-mini";
			promptMessage.Add(new LlmMessage("user", goal.GetGoalAsString()));
			var llmRequest = new LlmRequest("GoalDescription", promptMessage, model);

			(var result, var queryError) = await llmServiceFactory.CreateHandler().Query(llmRequest, responseType);
			if (queryError is IBuilderError builderError) return (goal, builderError);
			if (queryError != null) return (goal, new GoalBuilderError(queryError, goal, false));

			var goalDescription = result as GoalDescription;
			if (goalDescription == null)
			{
				return (goal, new GoalBuilderError("Could not create description for goal", goal));
			}

			goal.Description = goalDescription.Description;
			goal.IncomingVariablesRequired = goalDescription.IncomingVariablesRequired;
			logger.LogDebug($" - Done creating desription for goal {goal.GoalName}");
			return (goal, null);
		}

		/*
		public async Task<(DataSource? DataSource, IError? Error)> GetOrCreateDataSource(string? name)
		{

			if (string.IsNullOrEmpty(name))
			{
				return await dbSettings.GetDefaultDataSource();
			}

			var dataSourceResult = await dbSettings.GetDataSource(name);
			if (dataSourceResult.DataSource != null) return dataSourceResult;

			return await dbSettings.CreateDataSource(name);
		}*/

		private void RemoveUnusedPrFiles(Goal goal)
		{
			if (!fileSystem.Directory.Exists(goal.AbsolutePrFolderPath)) return;
			var files = fileSystem.Directory.GetFiles(goal.AbsolutePrFolderPath, "*.pr");
			foreach (var file in files)
			{
				string fileNameInGoalFolder = Path.GetFileName(file);
				if (fileNameInGoalFolder.StartsWith(ISettings.GoalFileName)) continue;

				if (goal.GoalSteps.FirstOrDefault(p => p.PrFileName == fileNameInGoalFolder) == null)
				{
					fileSystem.File.Delete(file);
				}
			}

			var dirs = fileSystem.Directory.GetDirectories(goal.AbsolutePrFolderPath).Select(p =>
					p.Replace(goal.AbsoluteAppStartupFolderPath, "")
					.TrimStart(fileSystem.Path.DirectorySeparatorChar)).ToList();

			foreach (var subGoal in goal.SubGoals)
			{
				dirs.Remove(fileSystem.Path.GetDirectoryName(subGoal) ?? "");
			}

			foreach (var dir in dirs)
			{
				fileSystem.Directory.Delete(dir, true);
			}
			int i = 0;

		}
		private bool GoalNameContainsMethod(Goal goal)
		{
			var goalName = goal.GoalName.ToUpper() + " " + goal.Comment;
			var match = Regex.Match(goalName, @"\s+(GET|POST|DELETE|PATCH|OPTION|HEAD|PUT)($|.*)");
			return match.Success;
		}

	}

}
