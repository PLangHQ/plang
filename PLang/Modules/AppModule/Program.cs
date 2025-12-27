using PLang.Building.Model;
using PLang.Building.Parsers;
using PLang.Errors;
using PLang.Errors.Runtime;
using PLang.Interfaces;
using PLang.Models;
using PLang.Runtime;
using PLang.SafeFileSystem;
using PLang.Services.AppsRepository;
using PLang.Utils;
using System.ComponentModel;

namespace PLang.Modules.AppModule;


[Description("Call an App. When the user has the word 'app' in his statement, this should be called. ")]
public class Program(IPseudoRuntime pseudoRuntime, IEngine engine, PrParser prParser, IPLangContextAccessor contextAccessor) : BaseProgram()
{
	public PLangContext Context { get { return context; } }

	[Description("Call/Runs another app. app can be located in another directory, then path points the way. goalName is default \"Start\" when it cannot be mapped")]
	public async Task<(object? Variables, IError? Error)> RunApp(AppToCallInfo appToCall)
	{
		var appRootPath = ResolveAppPath(appToCall.AppName);
		if (appRootPath == null)
		{
			return (null, new ProgramError($"Could not find app {appToCall.AppName}", goalStep, function));
		}

		IEngine appEngine = engine.RentAppEngine(appRootPath, !appToCall.IsAsync);

		// Step 3: Load goal using app engine's prParser
		var goal = appEngine.PrParser.GetGoal(appToCall.Path);
		if (goal == null)
		{
			engine.Return(appEngine);
			return (null, new ProgramError($"Path '{appToCall.Path}' in {appToCall.AppName} could not be found"));
		}

		if (appToCall.Parameters != null)
		{
			foreach (var param in appToCall.Parameters)
			{
				appEngine.Context.MemoryStack.Put(param.Key, param.Value);
			}
		}

		var task = RunAppInternal(appEngine, engine, goal, appToCall);

		if (!appToCall.IsAsync)
		{
			return await task;
		}

		return (task, null);
	}

	private string? ResolveAppPath(string appName)
	{
		// Local first: c:\plangapp1\apps\ide
		var localPath = fileSystem.Path.Combine(engine.FileSystem.RootDirectory, "apps", appName);
		if (fileSystem.Directory.Exists(localPath))
		{
			return localPath;
		}

		// Global: c:\plang\os\apps\ide (or wherever os lives)
		var globalPath = fileSystem.Path.Combine(engine.FileSystem.OsDirectory, "apps", appName);
		if (fileSystem.Directory.Exists(globalPath))
		{
			return globalPath;
		}

		return null;
	}

	private async Task<(object? Variables, IError? Error)> RunAppInternal(
		IEngine appEngine,
		IEngine parentEngine,
		Goal goal,
		AppToCallInfo appToCall)
	{
		try
		{
			if (appToCall.WaitBeforeExecutingInMs > 0)
			{
				await Task.Delay(appToCall.WaitBeforeExecutingInMs);
			}

			var result = await appEngine.RunGoal(goal, appEngine.Context);

			if (appToCall.AfterExecution != null)
			{
				await parentEngine.RunGoal(appToCall.AfterExecution, goal, parentEngine.Context);
			}

			return result;
		}
		catch (Exception ex)
		{
			var appError = new ProgramError(ex.Message, goalStep);
			if (appToCall.IsAsync)
			{
				await parentEngine.GetEventRuntime().RunGoalErrorEvents(goal, goalStep.Index, appError);
			}
			return (null, appError);
		}
		finally
		{
			parentEngine.Return(appEngine);
		}
	}

}
