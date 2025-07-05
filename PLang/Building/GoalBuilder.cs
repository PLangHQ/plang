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
using PLang.Interfaces;
using PLang.Models;
using PLang.Modules.DbModule;
using PLang.Runtime;
using PLang.Services.LlmService;
using PLang.Utils;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using System.Text.RegularExpressions;
using static PLang.Modules.BaseBuilder;
using static PLang.Modules.DbModule.ModuleSettings;

namespace PLang.Building
{

	public interface IGoalBuilder
	{
		Task<IBuilderError?> BuildGoal(IServiceContainer container, Goal goal, int errorCount = 0, int goalIndex = 0);
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
		private readonly PrParser prParser;
		private readonly ISettings settings;
		private readonly PLangAppContext context;
		private readonly ModuleSettings dbSettings;
		private readonly IInstructionBuilder instructionBuilder;
		private readonly VariableHelper variableHelper;
		private readonly IStepBuilder stepBuilder;
		public List<IBuilderError> BuildErrors { get; init; }
		public GoalBuilder(ILogger logger, IPLangFileSystem fileSystem, ILlmServiceFactory llmServiceFactory,
				IGoalParser goalParser, IStepBuilder stepBuilder, IEventRuntime eventRuntime, ITypeHelper typeHelper,
				PrParser prParser, ISettings settings, PLangAppContext context, Modules.DbModule.ModuleSettings dbSettings,
				IInstructionBuilder instructionBuilder, VariableHelper variableHelper)
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
			this.context = context;
			this.dbSettings = dbSettings;
			this.instructionBuilder = instructionBuilder;
			this.variableHelper = variableHelper;
			BuildErrors = new();
		}



		public async Task<IBuilderError?> BuildGoal(IServiceContainer container, Goal goal, int errorCount = 0, int goalIndex = 0)
		{
			GroupedBuildErrors groupedBuildErrors = new();
			if (!goal.HasChanged)
			{
				bool hasChanged = goal.GoalSteps.Any(p => string.IsNullOrEmpty(p.AbsolutePrFilePath));
				if (!hasChanged)
				{
					var validationError = await ValidateSteps(goal);
					if (validationError == null) return null;
				}
			}

			logger.LogInformation($"\nStart to build {goal.GoalName} - {goal.RelativeGoalPath} - {goal.HasChanged}");

			// Generate description and other properties for goal			
			(goal, var error) = await LoadMethodAndDescription(goal);
			if (error != null)
			{
				var result = await eventRuntime.RunGoalErrorEvents(goal, 0, error, true);
				if (result.Error != null) return new GoalBuilderError(result.Error, goal, ContinueBuild: false);

				return await BuildGoal(container, goal, errorCount, goalIndex);
			}

			var (vars, buildEventError) = await eventRuntime.RunBuildGoalEvents(EventType.Before, goal);
			if (!ContinueBuildGoal(buildEventError, goal))
			{
				return buildEventError;
			}

			for (int i = 0; i < goal.GoalSteps.Count; i++)
			{
				if (goal.GoalSteps[i].IsValid) continue;

				var buildStepError = await BuildStep(goal, i);
				if (buildStepError != null)
				{
					if (!buildStepError.ContinueBuild) return buildStepError;
					groupedBuildErrors.Add(buildStepError);
				}

			}
			RemoveUnusedPrFiles(goal);
			RegisterForPLangUserInjections(container, goal);

			(vars, buildEventError) = await eventRuntime.RunBuildGoalEvents(EventType.After, goal);
			if (!ContinueBuildGoal(buildEventError, goal))
			{
				return buildEventError;
			}

			WriteToGoalPrFile(goal);
			logger.LogInformation($"Done building goal {goal.GoalName}");

			return (groupedBuildErrors.Count > 0) ? groupedBuildErrors : null;
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

			goal.GoalSteps[i].Hash = JsonConvert.SerializeObject(goal.GoalSteps[i], GoalSerializer.Settings).ComputeHash().Hash;
			WriteToGoalPrFile(goal);

			return null;
		}

		private bool ContinueBuildGoal(IBuilderError? buildStepError, Goal goal)
		{
			if (buildStepError == null) return true;

			if (buildStepError.Goal == null) buildStepError.Goal = goal;

			BuildErrors.Add(buildStepError);

			logger.LogWarning($"  - ❌ Error building goal - {buildStepError.MessageOrDetail}");

			return buildStepError.ContinueBuild;
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
			else
			{
				logger.LogError($"  - ❌ Error finding module for step. I tried {string.Join(",", excludeModules)} - Error message: {buildStepError.MessageOrDetail}");
			}

			BuildErrors.Add(buildStepError);

			return buildStepError;
		}

		private async Task<GroupedBuildErrors?> ValidateSteps(Goal goal)
		{
			GroupedBuildErrors errors = new();
			foreach (var step in goal.GoalSteps)
			{

				var functionResult = step.GetFunction(fileSystem);
				if (functionResult.Error != null)
				{
					step.IsValid = false;
					step.Reload = true;
					errors.Add(functionResult.Error);

					// skip rest of validation, we already know this step has invalid build
					continue;
				}

				var methodHelper = new MethodHelper(step, variableHelper, typeHelper, logger);
				(var parameterProperties, var returnObjectsProperties, var invalidFunctionError) = methodHelper.ValidateFunctions(step.Instruction.Function, step.ModuleType, null);
				if (invalidFunctionError != null)
				{
					step.Reload = true;
					step.IsValid = false;
					errors.Add(invalidFunctionError);

					// skip rest of validation, we already know this step has invalid build
					continue;
				}

				var builderRun = await instructionBuilder.RunBuilderMethod(step, step.Instruction, functionResult.Function);
				if (builderRun.Error != null)
				{
					errors.Add(builderRun.Error);
					step.Reload = true;
					step.IsValid = false;

					// skip rest of validation, we already know this step has invalid build
					continue;
				}

				step.IsValid = true;

			}
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

			var assembly = Assembly.GetAssembly(this.GetType());
			goal.BuilderVersion = assembly.GetName().Version.ToString();
			goal.Hash = "";

			var json = JsonConvert.SerializeObject(goal, GoalSerializer.Settings);
			goal.Hash = json.ComputeHash().Hash;

			json = JsonConvert.SerializeObject(goal, GoalSerializer.Settings);
			fileSystem.File.WriteAllText(goal.AbsolutePrFilePath, json);
			return null;
		}

		private async Task<(Goal, IBuilderError?)> LoadMethodAndDescription(Goal goal)
		{
			Goal? oldGoal = null;
			if (fileSystem.File.Exists(goal.AbsolutePrFilePath))
			{
				oldGoal = JsonHelper.ParseFilePath<Goal>(fileSystem, goal.AbsolutePrFilePath);
				if (oldGoal != null)
				{
					goal.GoalInfo = oldGoal.GoalInfo;
				}
			}
			var isWebApiMethod = GoalNameContainsMethod(goal) || goal.RelativeGoalFolderPath.Contains(Path.DirectorySeparatorChar + "api");

			if (!isWebApiMethod)
			{
				return await CreateDescriptionForGoal(goal, oldGoal);
			}
			if (goal.GoalInfo == null || goal.GoalInfo.GoalApiInfo == null || goal.Text == null || goal.Text != oldGoal?.Text)
			{
				var promptMessage = new List<LlmMessage>();
				promptMessage.Add(new LlmMessage("system", $@"
GoalApiIfo:
	Determine the Method and write description of this api, using the content of the file.
	Method can be: GET, POST, DELETE, PUT, PATCH, OPTIONS, HEAD. The content will describe a function in multiple steps.
	From the first line, you should extrapolate the CacheControl if the user defines it.
	ContentType: can be application/json, text/html, etc
	CacheControlPrivateOrPublic: public or private
	NoCacheOrNoStore: no-cache or no-store"));
				promptMessage.Add(new LlmMessage("user", goal.GetGoalAsString()));
				var llmRequest = new LlmRequest("GoalApiInfo", promptMessage);

				(var result, var queryError) = await llmServiceFactory.CreateHandler().Query<Model.GoalInfo>(llmRequest);
				if (queryError != null) return (goal, new GoalBuilderError(queryError, goal, false));

				if (result != null)
				{
					goal.GoalInfo = result;
				}

			}
			return (goal, null);
		}

		public record GoalDescription(string Description, Dictionary<string, string>? IncomingVariablesRequired = null);

		private async Task<(Goal, IBuilderError?)> CreateDescriptionForGoal(Goal goal, Goal? oldGoal)
		{
			if (!string.IsNullOrEmpty(goal.Description) && goal.GetGoalAsString() == oldGoal?.GetGoalAsString())
			{
				return (goal, null);
			}

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

			var dirs = fileSystem.Directory.GetDirectories(goal.AbsolutePrFolderPath);
			foreach (var dir in dirs)
			{
				foreach (var subGoal in goal.SubGoals)
				{

					dirs = dirs.Where(p => !p.StartsWith(Path.Join(goal.AbsolutePrFolderPath, subGoal))).ToArray();
				}
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
