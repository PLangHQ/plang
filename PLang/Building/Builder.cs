﻿using LightInject;
using Microsoft.Extensions.Logging;
using PLang.Building.Parsers;
using PLang.Container;
using PLang.Errors.Handlers;
using PLang.Events;
using PLang.Exceptions;
using PLang.Exceptions.AskUser;
using PLang.Interfaces;
using PLang.Resources;
using PLang.Runtime;
using PLang.Services.OutputStream;
using PLang.Utils;
using System.Diagnostics;
using System.IO.Compression;
using PLang.Errors;
using PLang.Errors.Runtime;
using System.ComponentModel;
using PLang.Errors.Builder;
using System.Reactive.Concurrency;
using PLang.Errors.AskUser;
using PLang.SafeFileSystem;
using PLang.Building.Model;
using PLang.Modules.DbModule;
using Microsoft.Data.Sqlite;

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

		public Builder(ILogger logger, IPLangFileSystem fileSystem, ISettings settings, IGoalBuilder goalBuilder,
			IEventBuilder eventBuilder, IEventRuntime eventRuntime,
			PrParser prParser, IErrorHandlerFactory exceptionHandlerFactory, IAskUserHandlerFactory askUserHandlerFactory)
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
		}


		public async Task<IError?> Start(IServiceContainer container, string? absoluteGoalPath = null)
		{
			try
			{

				Stopwatch stopwatch = Stopwatch.StartNew();
				AppContext.SetSwitch("Builder", true);

				var goalFiles = GoalHelper.GetGoalFilesToBuild(fileSystem, fileSystem.GoalsPath);
				if (absoluteGoalPath != null)
				{
					goalFiles = goalFiles.Where(p => p.Equals(absoluteGoalPath)).ToList();
				}
				InitFolders();
				logger.LogInformation("Build Start:" + DateTime.Now.ToLongTimeString());

				(var eventGoalFiles, var error) = await eventBuilder.BuildEventsPr();
				if (error != null) return error;

				error = await eventRuntime.LoadBuilder(container.GetInstance<MemoryStack>());
				if (error != null) return error;

				var eventError = await eventRuntime.RunStartEndEvents(EventType.Before, EventScope.StartOfApp, true);
				if (eventError != null)
				{
					if (!eventError.IgnoreError)
					{
						return eventError;
					} else
					{
						logger.LogError(eventError.ToString());
					}
				}

				foreach (string file in goalFiles)
				{
					var goalError = await goalBuilder.BuildGoal(container, file);
					if (goalError != null && !goalError.ContinueBuild)
					{
						return goalError;
					}
					else if (goalError != null)
					{
						logger.LogWarning(goalError.ToFormat().ToString());
					}
				}

				
				CleanGoalFiles();

				eventError = await eventRuntime.RunStartEndEvents(EventType.After, EventScope.EndOfApp, true);
				if (eventError != null && !eventError.IgnoreError)
				{
					return eventError;
				}
				else if (eventError != null)
				{
					logger.LogWarning(eventError.ToFormat().ToString());
				}
				CleanUp();
				ShowBuilderErrors(goalFiles, stopwatch);

				
				

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

		private void CleanUp()
		{
			object? obj = AppContext.GetData("AnchorMemoryDb");
			if (obj != null && obj is SqliteConnection connection)
			{
				connection.Close();
				connection.Dispose();
			}
		}

		private void ShowBuilderErrors(List<string> goalFiles, Stopwatch stopwatch)
		{
			if (goalBuilder.BuildErrors.Count > 0)
			{
				foreach (var buildError in goalBuilder.BuildErrors)
				{
					logger.LogWarning(buildError.ToFormat().ToString());
				}

				logger.LogError($"\n\n❌ Failed to build {goalBuilder.BuildErrors.Count} steps");

			} else
			{				
				logger.LogWarning($"\n\n🎉 Build was succesfull!");
			}

			if (goalFiles.Count == 0)
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
