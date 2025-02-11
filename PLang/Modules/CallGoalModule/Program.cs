using LightInject;
using PLang.Building.Model;
using PLang.Building.Parsers;
using PLang.Container;
using PLang.Errors;
using PLang.Errors.Handlers;
using PLang.Errors.Runtime;
using PLang.Exceptions;
using PLang.Exceptions.AskUser;
using PLang.Interfaces;
using PLang.Models;
using PLang.Runtime;
using PLang.SafeFileSystem;
using PLang.Services.AppsRepository;
using PLang.Services.OutputStream;
using PLang.Utils;
using System.ComponentModel;
using static PLang.Executor;

namespace PLang.Modules.CallGoalModule
{
	[Description("Call another Goal or App, when ! is prefixed, example: call !RenameFile, call app !Google/Search, call !ui/ShowItems, call goal !DoStuff")]
	public class Program(IPseudoRuntime pseudoRuntime, IEngine engine, IPLangAppsRepository appsRepository, PrParser prParser, 
		IPLangFileSystem fileSystem, IFileAccessHandler fileAccessHandler, IServiceContainerFactory serviceContainerFactory,
		IOutputStreamFactory outputStreamFactory, IOutputSystemStreamFactory outputSystemStreamFactory,
			IErrorHandlerFactory errorHandlerFactory, IErrorSystemHandlerFactory errorSystemHandlerFactory,
			IAskUserHandlerFactory askUserHandlerFactory
		) : BaseProgram()
	{

		[Description("Call/Runs another app. app can be located in another directory, then path points the way. goalName is default \"Start\" when it cannot be mapped")]
		public async Task<IError?> RunApp(string appName, string? path = null, GoalToCall? goalName = null, Dictionary<string, object?>? parameters = null, bool waitForExecution = true,
			int delayWhenNotWaitingInMilliseconds = 50, uint waitForXMillisecondsBeforeRunningGoal = 0, bool keepMemoryStackOnAsync = false)
		{
			if (string.IsNullOrEmpty(appName))
			{
				return new ProgramError($"App name is missing from step: {goalStep.Text}", goalStep, function);
			}

			if (!fileSystem.Directory.Exists(fileSystem.Path.Join(path, appName))) {
				return new ProgramError($"App name is could not be found at: {fileSystem.Path.Join(path, appName)}", goalStep, function);
			}
			if (string.IsNullOrEmpty(goalName?.ToString())) goalName = new GoalToCall("Start");

			await prParser.LoadAppPath(appName, fileAccessHandler);

			string absoluteAppPath = fileSystem.Path.GetFullPath(fileSystem.Path.Join(fileSystem.RootDirectory, path, appName, "/").AdjustPathToOs());
			var goals = prParser.GetAllGoals().Where(p => p.AbsoluteAppStartupFolderPath == absoluteAppPath);
			var goalToCall = goals.FirstOrDefault(p => p.GoalName == goalName);
			
			if (goalToCall == null)
			{
				return new ProgramError($"Could not find goal {goalName} in app: {fileSystem.Path.Join(path, appName)}", goalStep, function);
			}
			
			var container = serviceContainerFactory.CreateContainer(context, absoluteAppPath, "./", outputStreamFactory, outputSystemStreamFactory,
			errorHandlerFactory, errorSystemHandlerFactory, askUserHandlerFactory);

			var engine = container.GetInstance<IEngine>();
			engine.Init(container);

			var ms = engine.GetMemoryStack();
			if (parameters != null)
			{
				foreach (var parameter in parameters)
				{
					ms.Put(parameter.Key, parameter.Value);
				}
			}

			var task = engine.RunGoal(goalToCall, waitForXMillisecondsBeforeRunningGoal);
			await task.ConfigureAwait(waitForExecution);

			return task.Result;
		}


		[Description("Call/Runs another goal. goalName can be prefixed with !. If backward slash(\\) is used by user, change to forward slash(/)")]
		public async Task<IError?> RunGoal(GoalToCall goalName, Dictionary<string, object?>? parameters = null, bool waitForExecution = true, 
			int delayWhenNotWaitingInMilliseconds = 50, uint waitForXMillisecondsBeforeRunningGoal = 0, bool keepMemoryStackOnAsync = false)
		{
			if (string.IsNullOrEmpty(goalName))
			{
				throw new RuntimeException($"Goal name is missing from step: {goalStep.Text}");
			}

			ValidateAppInstall(goalName);
			

			var result = await pseudoRuntime.RunGoal(engine, context, Goal.RelativeAppStartupFolderPath, goalName,
					variableHelper.LoadVariables(parameters), Goal, 
					waitForExecution, delayWhenNotWaitingInMilliseconds, waitForXMillisecondsBeforeRunningGoal, goalStep.Indent, keepMemoryStackOnAsync);
			return result.error;
			
		}


		private void ValidateAppInstall(string goalToRun)
		{
			if (string.IsNullOrEmpty(goalToRun)) return;
			if (!goalToRun.Contains("apps")) return;

			goalToRun = goalToRun.AdjustPathToOs().Replace("!", "");

			string appName = GoalHelper.GetAppName(goalToRun);
			string goalName = GoalHelper.GetGoalPath(goalToRun);

			string buildPath = fileSystem.Path.Join(fileSystem.Path.DirectorySeparatorChar.ToString(), "apps", appName, ".build", goalName);
			var goal = prParser.GetAllGoals().FirstOrDefault(p => p.RelativePrFolderPath.ToLower() == buildPath.ToLower());
			if (goal != null) return;

			appsRepository.InstallApp(appName);


		}


	}


}

