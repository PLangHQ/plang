using Nethereum.ABI.CompilationMetadata;
using PLang.Building;
using PLang.Errors;
using PLang.Events;
using PLang.Exceptions;
using PLang.Runtime;
using PLang.Utils;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static PLang.Runtime.PseudoRuntime;

namespace PLang.Models;

public partial class App
{

	public async Task<IError?> Build()
	{
		try
		{
			var builder = Container.GetInstance<IBuilder>();
			var error = await builder.Start(Container);
			if (error != null)
			{
				return error;
			}
			return null;
		}
		catch (Exception ex)
		{
			return new ExceptionError(ex);

		}
	}

	public async Task<(object?, IError?)> Start(GoalToCallInfo toCallInfo)
	{
		/*
		 * þarf að setja SetupLoaded = true, EventsLoaded = true, loada setup og events á new app instance
		 * plang -> Start.goal
		 * plang apps/Ble/ -> apps/Ble/Start.goal
		 * plang apps/Ble/apps/Bla -> apps/Ble/apps/Bla/Start.goal
		 * 
		 * GoalToCallInfo should have isolated property
		 * 
		 * threaded 16 cpus
		 * 
		 * app.CallGoal(goalName, parameters, rent=true)
		 *   = nytt engine með nýju memory stack, etc.
		 */
		Context.Add("Runtime", true);

		Output.Write("App Start:" + DateTime.Now.ToLongTimeString(), type: "info", channel: "log");

		var result = Engine.RunSetup();
		if (result.Error != null) return result.Error;

		var result = Engine.StartAppEvents();
		if (result.Error != null) return result.Error;

		var result = Engine.RunGoal(toCallInfo);
		if (result.Error != null) return result.Error;

		var result = Engine.EndAppEvents();
		if (result.Error != null) return result.Error;

		AppContext.SetSwitch("Runtime", true);
		try
		{
			logger.LogInformation("App Start:" + DateTime.Now.ToLongTimeString());

			var error = eventRuntime.Load(false);
			if (error != null)
			{
				await HandleError(error);
				return;
			}

			var eventResult = await eventRuntime.RunStartEndEvents(EventType.Before, EventScope.StartOfApp);
			if (eventResult.Error != null)
			{
				await HandleError(eventResult.Error);
				return;
			}

			error = await RunSetup();
			if (error != null)
			{
				await HandleError(error);
				return;
			}
			if (goalsToRun.Count == 1 && goalsToRun[0].ToLower().RemoveExtension() == "setup") return;


			error = await RunStart(goalsToRun);
			if (error != null)
			{
				await HandleError(error);
				return;
			}


			eventResult = await eventRuntime.RunStartEndEvents(EventType.After, EventScope.StartOfApp);
			if (eventResult.Error != null)
			{
				await HandleError(eventResult.Error);
				return;
			}

			WatchForRebuild();
		}
		catch (Exception ex)
		{
			logger.LogError(ex, "OnStart");
			var error = new Error(ex.Message, Exception: ex);
			await HandleError(error);

		}
		finally
		{
			var alives = AppContext.GetData("KeepAlive") as List<Alive>;
			if (alives != null && alives.Count > 0)
			{
				logger.LogWarning("Keeping app alive, reasons:");
				foreach (var alive in alives)
				{
					logger.LogWarning(" - " + alive.Key);
				}

				while (alives != null && alives.Count > 0)
				{
					await Task.Delay(1000);
					alives = AppContext.GetData("KeepAlive") as List<Alive>;
					if (alives != null && alives.Count > 0)
					{
						var aliveTaskType = alives.FirstOrDefault(p => p.Key == "WaitForExecution");
						if (aliveTaskType?.Instances != null)
						{
							bool isCompleted = true;

							List<Task> tasks = new();
							for (int i = 0; i < aliveTaskType.Instances.Count; i++)
							{
								var engineWait = (EngineWait)aliveTaskType.Instances[i];
								tasks.Add(engineWait.task);

								await engineWait.task.ConfigureAwait(false);
								if (engineWait.task.IsFaulted)
								{
									Console.WriteLine(engineWait.task.Exception.Flatten().ToString());
								}
								aliveTaskType.Instances.Remove(engineWait);
								engineWait.engine.ParentEngine?.GetEnginePool(engineWait.engine.Path).Return(engineWait.engine);
								i--;

							}
							if (aliveTaskType.Instances.Count == 0)
							{
								alives.Remove(aliveTaskType);
							}
							/*
							if (!engineWait.task.IsCompleted)
								{

									isCompleted = false;
								}
								else
								{
									engineWait.engine.ParentEngine?.GetEnginePool(engineWait.engine.Path).Return(engineWait.engine);

									aliveTaskType.Instances.Remove(engineWait);
								}
							}
							if (isCompleted)
							{
								alives.Remove(aliveTaskType);
							}*/
						}
					}
				}
			}

			var eventResult = await eventRuntime.RunStartEndEvents(EventType.Before, EventScope.EndOfApp);
			if (eventResult.Error != null)
			{
				await HandleError(eventResult.Error);
			}
		}
	}

	public void RegisterArgs(List<string> args)
	{
		Context.AddOrReplace(ReservedKeywords.ParametersAtAppStart, args.Where(p => p.StartsWith("--")).ToArray());
		if (args.FirstOrDefault(p => p == "--debug") != null)
		{
			Context.AddOrReplace(ReservedKeywords.Debug, true);
			Context.AddOrReplace(ReservedKeywords.DetailedError, true);
		}
		var csdebug = args.FirstOrDefault(p => p == "--csdebug") != null;
		if (csdebug)
		{
			if (!Debugger.IsAttached) Debugger.Launch();
			Context.AddOrReplace(ReservedKeywords.CSharpDebug, true);
			Context.AddOrReplace(ReservedKeywords.DetailedError, true);
		}
		var strictbuild = args.FirstOrDefault(p => p == "--strictbuild") != null;
		if (strictbuild)
		{
			Context.AddOrReplace(ReservedKeywords.StrictBuild, true);
		}
		var detailerror = args.FirstOrDefault(p => p == "--detailerror") != null;
		if (detailerror)
		{
			Context.AddOrReplace(ReservedKeywords.DetailedError, true);
		}
		var loggerLovel = args.FirstOrDefault(p => p.StartsWith("--logger"));
		if (loggerLovel != null)
		{
			Context.AddOrReplace("--logger", loggerLovel.Replace("--logger=", ""));
		}


		var llmservice = args.FirstOrDefault(p => p.ToLower().StartsWith("--llmservice")) ?? Environment.GetEnvironmentVariable("PLangLllmService");
		if (!string.IsNullOrEmpty(llmservice))
		{
			var serviceName = llmservice.ToLower();
			if (llmservice.IndexOf("=") != -1)
			{
				serviceName = llmservice.Substring(llmservice.IndexOf("=") + 1).ToLower();
			}

			if (serviceName != "plang" && serviceName != "openai")
			{
				throw new RuntimeException("Parameter --llmservice can only be 'plang' or 'openai'. For example --llmservice=openai");
			}
			Context.AddOrReplace("llmservice", serviceName);
		}
	}
}

