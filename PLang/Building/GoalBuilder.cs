using LightInject;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using PLang.Building.Events;
using PLang.Building.Model;
using PLang.Building.Parsers;
using PLang.Container;
using PLang.Exceptions;
using PLang.Interfaces;
using PLang.Models;
using PLang.Services.LlmService;
using PLang.Services.SettingsService;
using PLang.Utils;
using System.ComponentModel;
using System.Reflection;
using System.Text.RegularExpressions;

namespace PLang.Building
{

    public interface IGoalBuilder
	{
		Task BuildGoal(IServiceContainer container, string goalFileAbsolutePath, int errorCount = 0);
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
		private readonly IStepBuilder stepBuilder;

		public GoalBuilder(ILogger logger, IPLangFileSystem fileSystem, ILlmServiceFactory llmServiceFactory,
				IGoalParser goalParser, IStepBuilder stepBuilder, IEventRuntime eventRuntime, ITypeHelper typeHelper, PrParser prParser)
		{

			this.fileSystem = fileSystem;
			this.llmServiceFactory = llmServiceFactory;
			this.logger = logger;
			this.goalParser = goalParser;
			this.stepBuilder = stepBuilder;
			this.eventRuntime = eventRuntime;
			this.typeHelper = typeHelper;
			this.prParser = prParser;
		}
		public async Task BuildGoal(IServiceContainer container, string goalFileAbsolutePath, int errorCount = 0)
		{
			var goals = goalParser.ParseGoalFile(goalFileAbsolutePath);
			if (goals == null || goals.Count == 0)
			{
				logger.LogWarning($"Could not determine goal on {Path.GetFileName(goalFileAbsolutePath)}.");
				return;
			}

			for (int b = 0; b < goals.Count; b++)
			{
				var goal = goals[b];
				logger.LogInformation($"\nStart to build {goal.GoalName}");
				// if this api, check for http method. Also give description.					
				goal = await LoadMethodAndDescription(goal);

				await eventRuntime.RunBuildGoalEvents(EventType.Before, goal);

				for (int i = 0; i < goal.GoalSteps.Count; i++)
				{
					await stepBuilder.BuildStep(goal, i);

					WriteToGoalPrFile(goal);
				}
				RemoveUnusedPrFiles(goal);
				RegisterForPLangUserInjections(container, goal);

				await eventRuntime.RunBuildGoalEvents(EventType.After, goal);

				WriteToGoalPrFile(goal);
				logger.LogInformation($"Done building goal {goal.GoalName}");
			}
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
			goal.Hash = JsonConvert.SerializeObject(goal).ComputeHash();
			
			fileSystem.File.WriteAllText(goal.AbsolutePrFilePath, JsonConvert.SerializeObject(goal, Formatting.Indented));
		}

		private async Task<Goal> LoadMethodAndDescription(Goal goal)
		{
			Goal? oldGoal = null;
			if (fileSystem.File.Exists(goal.AbsolutePrFilePath))
			{
				oldGoal = JsonHelper.ParseFilePath<Goal>(fileSystem, goal.AbsolutePrFilePath);
				if (oldGoal != null && oldGoal.GoalApiInfo != null)
				{
					goal.GoalApiInfo = oldGoal.GoalApiInfo;
				}
			}

			var isWebApiMethod = GoalNameContainsMethod(goal) || goal.RelativeGoalFolderPath.Contains(Path.DirectorySeparatorChar + "api");

			if (isWebApiMethod && (goal.GoalApiInfo == null || goal.GoalName != oldGoal?.GoalName))
			{
				var promptMessage = new List<LlmMessage>();
				promptMessage.Add(new LlmMessage("system", $@"Determine the Method and write description of this api, using the content of the file.
Method can be: GET, POST, DELETE, PUT, PATCH, OPTIONS, HEAD. The content will describe a function in multiple steps.
From the first line, you should extrapolate the CacheControl if the user defines it.
CacheControlPrivateOrPublic: public or private
NoCacheOrNoStore: no-cache or no-store"));
				promptMessage.Add(new LlmMessage("user", goal.GetGoalAsString()));
				var llmRequest = new LlmRequest("GoalApiInfo", promptMessage);


				var result = await llmServiceFactory.CreateHandler().Query<GoalApiInfo>(llmRequest);
				if (result != null)
				{
					goal.GoalApiInfo = result;
				}

			}
			return goal;
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
		}
		private bool GoalNameContainsMethod(Goal goal)
		{
			var goalName = goal.GoalName.ToUpper();
			var match = Regex.Match(goalName, @"\s*(GET|POST|DELETE|PATCH|OPTION|HEAD|PUT)($|.*)");
			return match.Success;
		}

	}

}
