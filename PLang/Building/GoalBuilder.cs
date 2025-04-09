using LightInject;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using PLang.Building.Model;
using PLang.Building.Parsers;
using PLang.Container;
using PLang.Errors;
using PLang.Errors.Builder;
using PLang.Events;
using PLang.Interfaces;
using PLang.Models;
using PLang.Services.LlmService;
using PLang.Utils;
using System.Collections.Generic;
using System.Reflection;
using System.Text.RegularExpressions;
using static PLang.Modules.DbModule.ModuleSettings;

namespace PLang.Building
{

	public interface IGoalBuilder
	{
		Task<IBuilderError?> BuildGoal(IServiceContainer container, string goalFileAbsolutePath, int errorCount = 0);
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
		private readonly IStepBuilder stepBuilder;
		public List<IBuilderError> BuildErrors { get; init; }
		public GoalBuilder(ILogger logger, IPLangFileSystem fileSystem, ILlmServiceFactory llmServiceFactory,
				IGoalParser goalParser, IStepBuilder stepBuilder, IEventRuntime eventRuntime, ITypeHelper typeHelper,
				PrParser prParser, ISettings settings, PLangAppContext context)
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
			BuildErrors = new();
		}
		public async Task<IBuilderError?> BuildGoal(IServiceContainer container, string goalFileAbsolutePath, int errorCount = 0)
		{
			List<Goal> goals = goalParser.ParseGoalFile(goalFileAbsolutePath);

			if (goals == null || goals.Count == 0)
			{
				return new BuilderError($"Could not determine goal on {Path.GetFileName(goalFileAbsolutePath)}.");
			}

			for (int b = 0; b < goals.Count; b++)
			{
				var goal = goals[b];
				logger.LogInformation($"\nStart to build {goal.GoalName} - {goal.RelativeGoalPath}");

				// Generate description and other properties for goal			
				(goal, var error) = await LoadMethodAndDescription(goal);
				if (error != null) return error;

				var buildEventError = await eventRuntime.RunBuildGoalEvents(EventType.Before, goal);
				if (buildEventError != null && !buildEventError.ContinueBuild)
				{
					return buildEventError;
				}
				else if (buildEventError != null)
				{
					logger.LogWarning(buildEventError.ToFormat().ToString());
				}

				for (int i = 0; i < goal.GoalSteps.Count; i++)
				{
					var buildStepError = await stepBuilder.BuildStep(goal, i);
					if (buildStepError != null && !buildStepError.ContinueBuild)
					{
						if (buildStepError.Step == null) buildStepError.Step = goal.GoalSteps[i];
						if (buildStepError.Goal == null) buildStepError.Goal = goal;
						return buildStepError;
					}
					else if (buildStepError != null)
					{
						if (buildStepError.Step == null) buildStepError.Step = goal.GoalSteps[i];
						if (buildStepError.Goal == null) buildStepError.Goal = goal;
						BuildErrors.Add(buildStepError);
						logger.LogWarning(buildStepError.ToFormat().ToString());
					}
					else
					{
						goal.GoalSteps[i].Hash = JsonConvert.SerializeObject(goal.GoalSteps[i]).ComputeHash().Hash;
						WriteToGoalPrFile(goal);
					}
				}
				RemoveUnusedPrFiles(goal);
				RegisterForPLangUserInjections(container, goal);

				buildEventError = await eventRuntime.RunBuildGoalEvents(EventType.After, goal);
				if (buildEventError != null && !buildEventError.ContinueBuild)
				{
					return buildEventError;
				}
				else if (buildEventError != null)
				{
					logger.LogWarning(buildEventError.ToFormat().ToString());
				}

				if (!string.IsNullOrEmpty(goal.DataSourceName))
				{
					context.Remove(ReservedKeywords.CurrentDataSource);
				}
				WriteToGoalPrFile(goal);
				logger.LogInformation($"Done building goal {goal.GoalName}");
			}
			return null;
		}

		private void RegisterForPLangUserInjections(IServiceContainer container, Goal goal)
		{
			foreach (var injection in goal.Injections)
			{

				RegisterForPLangUserInjections(container, injection.Type, injection.Path, injection.IsGlobal, injection.EnvironmentVariable, injection.EnvironmentVariableValue);

			}
		}

		private void LoadInjections(Goal goal)
		{
			goal.Injections.RemoveAll(p => !p.AtSignInjection);

			var injectionSteps = goal.GoalSteps.Where(p => p.ModuleType == "PLang.Modules.InjectModule");
			foreach (var injection in injectionSteps)
			{
				var instruction = prParser.ParseInstructionFile(injection);
				if (instruction == null) continue;

				var gfs = instruction.GetFunctions();
				if (gfs != null && gfs.Length > 0)
				{
					var gf = gfs[0];

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

			}


		}

		private void RegisterForPLangUserInjections(IServiceContainer container, string type, string path, bool isGlobal, string? environmentVariable = null, string? environmentVariableValue = null)
		{
			container.RegisterForPLangUserInjections(type, path, isGlobal, environmentVariable, environmentVariableValue);
		}

		private void WriteToGoalPrFile(Goal goal)
		{
			if (!fileSystem.Directory.Exists(goal.AbsolutePrFolderPath))
			{
				fileSystem.Directory.CreateDirectory(goal.AbsolutePrFolderPath);
			}

			LoadInjections(goal);

			var assembly = Assembly.GetAssembly(this.GetType());
			goal.BuilderVersion = assembly.GetName().Version.ToString();
			goal.Hash = "";
			goal.Hash = JsonConvert.SerializeObject(goal).ComputeHash().Hash;

			fileSystem.File.WriteAllText(goal.AbsolutePrFilePath, JsonConvert.SerializeObject(goal, Formatting.Indented));
		}

		private async Task<(Goal?, IBuilderError?)> LoadMethodAndDescription(Goal goal)
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
				var llmRequest = new LlmRequest("GoalApiInfo", promptMessage, "gpt-4o-mini");

				(var result, var queryError) = await llmServiceFactory.CreateHandler().Query<GoalInfo>(llmRequest);
				if (queryError != null) return (goal, new BuilderError(queryError, false));

				if (result != null)
				{
					goal.GoalInfo = result;
				}

			}
			return (goal, null);
		}

		public record GoalDescription(string Description, string[]? IncomingVariablesRequired = null);
		public record GoalSetupDescription(string Description, string[]? IncomingVariablesRequired = null, string? DataSourceName = null) : GoalDescription(Description, IncomingVariablesRequired);

		private async Task<(Goal, IBuilderError?)> CreateDescriptionForGoal(Goal goal, Goal? oldGoal)
		{
			if (!string.IsNullOrEmpty(goal.Description) && goal.GetGoalAsString() == oldGoal?.GetGoalAsString())
			{
				var error = await PrepareSetup(goal);
				return (goal, error);
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
Analyze the goal and list out variables that are required to be sent to this goal to make it work, IncomingVariablesRequired
Step that writes into a variable are creating that variable
Describe conditions that are affected by variables, describe what value of variable should be to call goal or perform some steps
[llm] or step starting with system: and contains scheme:, will create variables from that scheme automatically, e.g. scheme: {{ name:string }} will create %name% variable
Be concise
"));
			Type responseType = typeof(GoalDescription);
			if (GoalHelper.IsSetup(goal))
			{
				promptMessage.Add(new LlmMessage("system", "datasource name might be defined with the title of the goal, e.g. Setup - name:/user/%id% => DataSourceName:/user/%id%. this is clearly defined by user. null when not defined"));
				responseType = typeof(GoalSetupDescription);
			}

			promptMessage.Add(new LlmMessage("user", goal.GetGoalAsString()));
			var llmRequest = new LlmRequest("GoalDescription", promptMessage, "gpt-4o-mini");

			(var result, var queryError) = await llmServiceFactory.CreateHandler().Query(llmRequest, responseType);
			if (queryError != null) return (goal, new GoalBuilderError(queryError, goal));

			var goalDescription = result as GoalDescription;
			if (goalDescription == null)
			{
				return (goal, new GoalBuilderError("Could not create description for goal", goal));
			}

			if (result is GoalSetupDescription setupResult)
			{
				if (!goal.GoalName.Equals("setup", StringComparison.OrdinalIgnoreCase))
				{
					var createdDataSourceResult = await GetOrCreateDataSource(setupResult.DataSourceName);
					if (createdDataSourceResult.Error != null) return (goal, new GoalBuilderError(createdDataSourceResult.Error, goal));

					goal.DataSourceName = createdDataSourceResult.DataSource?.Name;
					context.AddOrReplace(ReservedKeywords.CurrentDataSource, createdDataSourceResult.DataSource);
				}
			}
			goal.Description = goalDescription.Description;
			goal.IncomingVariablesRequired = goalDescription.IncomingVariablesRequired;

			return (goal, null);
		}

		private async Task<IBuilderError?> PrepareSetup(Goal goal)
		{
			if (!GoalHelper.IsSetup(goal)) return null;

			if (string.IsNullOrEmpty(goal.DataSourceName))
			{
				if (!goal.GoalName.Equals("setup", StringComparison.OrdinalIgnoreCase))
				{
					return new GoalBuilderError("Datasource name on setup file is empty. Delete the .pr file and rebuild", goal, ContinueBuild: false);
				}
			}

			var dataSourceResult = await GetOrCreateDataSource(goal.DataSourceName);
			if (dataSourceResult.Error != null) return new GoalBuilderError(dataSourceResult.Error, goal);

			context.AddOrReplace(ReservedKeywords.CurrentDataSource, dataSourceResult.DataSource);
			return null;
		}

		public async Task<(DataSource? DataSource, IError? Error)> GetOrCreateDataSource(string? name)
		{

			var dbSettings = new Modules.DbModule.ModuleSettings(fileSystem, settings, context, llmServiceFactory, logger, typeHelper);
			if (string.IsNullOrEmpty(name))
			{
				return await dbSettings.GetDefaultDataSource();
			}

			var dataSourceResult = await dbSettings.GetDataSource(name);
			if (dataSourceResult.DataSource != null) return dataSourceResult;

			return await dbSettings.CreateDataSource(name);
		}

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
			var match = Regex.Match(goalName, @"\s*(GET|POST|DELETE|PATCH|OPTION|HEAD|PUT)($|.*)");
			return match.Success;
		}

	}

}
