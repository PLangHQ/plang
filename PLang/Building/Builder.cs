﻿using LightInject;
using Microsoft.Extensions.Logging;
using PLang.Building.Model;
using PLang.Building.Parsers;
using PLang.Errors;
using PLang.Errors.AskUser;
using PLang.Errors.Handlers;
using PLang.Events;
using PLang.Exceptions;
using PLang.Exceptions.AskUser;
using PLang.Interfaces;
using PLang.SafeFileSystem;
using PLang.Utils;
using System.Data;
using System.Diagnostics;

namespace PLang.Building
{
	public interface IBuilder
	{
		Task<IError?> Start(IServiceContainer container, string? absoluteGoalPath = null);
	}
	public class Builder : IBuilder
	{
		private readonly IPLangFileSystem fileSystem;
		private readonly ILogger logger;
		private readonly ISettings settings;
		private readonly IGoalBuilder goalBuilder;
		private readonly IEventBuilder eventBuilder;
		private readonly IEventRuntime eventRuntime;
		private readonly PrParser prParser;
		private readonly IErrorHandlerFactory exceptionHandlerFactory;
		private readonly IAskUserHandlerFactory askUserHandlerFactory;
		private readonly IGoalParser goalParser;

		public Builder(ILogger logger, IPLangFileSystem fileSystem, ISettings settings, IGoalBuilder goalBuilder,
			IEventBuilder eventBuilder, IEventRuntime eventRuntime,
			PrParser prParser, IErrorHandlerFactory exceptionHandlerFactory, IAskUserHandlerFactory askUserHandlerFactory, 
			IGoalParser goalParser)
		{

			this.fileSystem = fileSystem;
			this.logger = logger;
			this.settings = settings;
			this.goalBuilder = goalBuilder;
			this.eventBuilder = eventBuilder;
			this.eventRuntime = eventRuntime;
			this.prParser = prParser;
			this.exceptionHandlerFactory = exceptionHandlerFactory;
			this.askUserHandlerFactory = askUserHandlerFactory;
			this.goalParser = goalParser;
		}


		public async Task<IError?> Start(IServiceContainer container, string? absoluteGoalPath = null)
		{
			try
			{
				Stopwatch stopwatch = Stopwatch.StartNew();
				AppContext.SetSwitch("Builder", true);
				Goal goal = Goal.Builder;

				logger.LogDebug($"Loading goal files - {stopwatch.ElapsedMilliseconds}");
				var goals = goalParser.GetGoalFilesToBuild();
				logger.LogDebug($"Done loading goal files now Init folder - {stopwatch.ElapsedMilliseconds}");

				InitFolders();

				logger.LogDebug($"Done Init folder, now load event runtime - {stopwatch.ElapsedMilliseconds}");
				logger.LogInformation("Build Start:" + DateTime.Now.ToLongTimeString());

				var error = eventRuntime.Load(true);
				if (error != null) return error;

				logger.LogDebug($"Done event runtime - {stopwatch.ElapsedMilliseconds}");
				
				var setupGoals = goals.Where(p => p.IsSetup).OrderBy(p => !p.GoalName.Equals("setup", StringComparison.OrdinalIgnoreCase));
				foreach (var setupGoal in setupGoals)
				{
					logger.LogDebug($"Start setup file build on '{setupGoal.GoalName}' - {stopwatch.ElapsedMilliseconds}");
					var goalError = await goalBuilder.BuildGoal(container, setupGoal);
					if (goalError != null && !goalError.ContinueBuild)
					{
						return goalError;
					}
					else if (goalError != null)
					{
						logger.LogWarning(goalError.ToFormat().ToString());
						goalBuilder.BuildErrors.Add(goalError);
					}
					logger.LogDebug($"Done Setup Build on {setupGoal.GoalName} - {stopwatch.ElapsedMilliseconds}");
				}

				if (absoluteGoalPath != null)
				{
					goals = goals.Where(p => p.AbsoluteGoalPath.Equals(absoluteGoalPath)).ToList();
				}
				logger.LogDebug($"Start building BuildEvents - {stopwatch.ElapsedMilliseconds}");
				(_, error) = await eventBuilder.BuildEventsPr();
				if (error != null) return error;
				logger.LogDebug($"Done building BuildEvents - {stopwatch.ElapsedMilliseconds}");

				var (_, eventError) = await eventRuntime.RunStartEndEvents(EventType.Before, EventScope.StartOfApp, goal, true);
				if (eventError != null)
				{
					return eventError;
				}
				
				goals = goals.Where(p => !p.IsSetup && !p.IsEvent).ToList();
				foreach (var goalToBuild in goals)
				{
					Stopwatch buildGoalTime = Stopwatch.StartNew();
					logger.LogDebug($"Building goal {goalToBuild.GoalName} - {stopwatch.ElapsedMilliseconds}");
					var goalError = await goalBuilder.BuildGoal(container, goalToBuild);
					if (goalError != null && !goalError.ContinueBuild)
					{
						return goalError;
					}
					else if (goalError != null)
					{
						goalBuilder.BuildErrors.Add(goalError);
						logger.LogWarning(goalError.ToFormat().ToString());
					}

					foreach (var subGoalPr in goalToBuild.SubGoals)
					{
						var subgoal = goals.FirstOrDefault(p => p.RelativePrPath == subGoalPr);
						if (subgoal != null)
						{
							subgoal.AddVariables(goalToBuild.GetVariables());
						}
					}
					logger.LogDebug($"Done building goal {goalToBuild.GoalName} took {buildGoalTime.ElapsedMilliseconds} - Total build time: {stopwatch.ElapsedMilliseconds}");
				}

				logger.LogDebug($"Cleaning up goal files - {stopwatch.ElapsedMilliseconds}");
				CleanGoalFiles();

				(_, eventError) = await eventRuntime.RunStartEndEvents(EventType.After, EventScope.EndOfApp, goal, true);
				if (eventError != null)
				{
					return eventError;
				}

				ReleaseDatabase();
				ShowBuilderErrors(goals, stopwatch);

				logger.LogDebug($"Done - Finished cleaning, db releasee and inform user - {stopwatch.ElapsedMilliseconds}");


			}
			catch (Exception ex)
			{
				if (ex is FileAccessException fa)
				{
					var fileAccessHandler = container.GetInstance<IFileAccessHandler>();
					var askUserFileAccess = new AskUserFileAccess(fa.AppName, fa.Path, fa.Message, fileAccessHandler.ValidatePathResponse);

					(var isFaHandled, var handlerError) = await askUserHandlerFactory.CreateHandler().Handle(askUserFileAccess);
					if (isFaHandled) return await Start(container);

					return ErrorHelper.GetMultipleError(askUserFileAccess, handlerError);
				}

				if (ex is MissingSettingsException mse)
				{
					var settingsError = new Errors.AskUser.AskUserError(mse.Message, async (object[]? result) =>
					{
						var value = result?[0] ?? null;
						if (value is Array) value = ((object[])value)[0];

						await mse.InvokeCallback(value);
						return (true, null);
					});

					(var isMseHandled, var handlerError) = await askUserHandlerFactory.CreateHandler().Handle(settingsError);
					if (isMseHandled) return await Start(container);
				}

				var step = (ex is BuilderStepException bse) ? bse.Step : null;
				var goal = (ex is BuilderException be) ? be.Goal : null;

				var error = new ExceptionError(ex, ex.Message, goal ?? step?.Goal, step, Key: ex.GetType().FullName);
				var handler = exceptionHandlerFactory.CreateHandler();
				(var isHandled, var handleError) = await handler.Handle(error);
				if (!isHandled)
				{
					if (handleError != null)
					{
						var me = new MultipleError(error);
						me.Add(handleError);
						await handler.ShowError(error, null);
					}
					else
					{
						await handler.ShowError(error, null);
					}
				}

			}
			return null;
		}

		private void ReleaseDatabase()
		{
			var anchors = AppContext.GetData("AnchorMemoryDb") as Dictionary<string, IDbConnection>;
			foreach (var anchor in anchors ?? [])
			{
				anchor.Value.Close();
				anchor.Value.Dispose();
			}

		}

		private void ShowBuilderErrors(List<Goal> goals, Stopwatch stopwatch)
		{
			if (goalBuilder.BuildErrors.Count > 0)
			{
				foreach (var buildError in goalBuilder.BuildErrors)
				{
					logger.LogWarning(buildError.ToFormat().ToString());
				}

				logger.LogError($"\n\n❌ Failed to build {goalBuilder.BuildErrors.Count} steps");

			}
			else
			{
				logger.LogWarning($"\n\n🎉 Build was succesfull!");
			}

			if (goals.Count == 0)
			{
				logger.LogInformation($"No goal files changed since last build - Time:{stopwatch.Elapsed.TotalSeconds.ToString("#,##.##")} sec - at {DateTime.Now}");
			}
			else
			{
				logger.LogInformation($"Build done - Time:{stopwatch.Elapsed.TotalSeconds.ToString("#,##.##")} sec - started at {DateTime.Now}");
			}
		}

		private void InitFolders()
		{
			var buildPath = fileSystem.Path.Join(fileSystem.RootDirectory, ".build");
			if (!fileSystem.Directory.Exists(buildPath))
			{
				var dir = fileSystem.Directory.CreateDirectory(buildPath);
				dir.Attributes = FileAttributes.Directory | FileAttributes.Hidden;
			}

			var dbPath = fileSystem.Path.Join(fileSystem.RootDirectory, ".db");
			if (!fileSystem.Directory.Exists(dbPath))
			{
				var dir = fileSystem.Directory.CreateDirectory(dbPath);
				dir.Attributes = FileAttributes.Directory | FileAttributes.Hidden;
			}
		}

		private void CleanGoalFiles()
		{
			var goals = prParser.ForceLoadAllGoals();

			List<Goal> goalsToRemove = new List<Goal>();
			foreach (var goal in goals)
			{
				if (!fileSystem.File.Exists(goal.AbsoluteGoalPath))
				{
					goalsToRemove.Add(goal);
				}
			}

			foreach (var goal in goalsToRemove)
			{
				if (fileSystem.Directory.Exists(goal.AbsolutePrFolderPath))
				{
					fileSystem.Directory.Delete(goal.AbsolutePrFolderPath, true);
				}
			}
			/*
			 * var dirs = fileSystem.Directory.GetDirectories(".build", "", SearchOption.AllDirectories);

			foreach (var goal in goals)
			{
				dirs = dirs.Where(p => !p.Equals(goal.AbsolutePrFolderPath, StringComparison.OrdinalIgnoreCase)).ToArray();
				dirs = dirs.Where(p => !goal.AbsolutePrFolderPath.StartsWith(p, StringComparison.OrdinalIgnoreCase)).ToArray();
			}

			foreach (var dir in dirs)
			{
				var folderPath = fileSystem.Path.Join(fileSystem.RootDirectory, dir.Replace(fileSystem.BuildPath, ""));
				if (!fileSystem.Directory.Exists(folderPath) && fileSystem.Directory.Exists(dir))
				{
					fileSystem.Directory.Delete(dir, true);
				}
			}
			*/
			var prGoalFiles = prParser.ForceLoadAllGoals();
			int i = 0;

		}

	}


}
