using LightInject;
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

namespace PLang.Building
{
	public interface IBuilder
	{
		Task<IError?> Start(IServiceContainer container);
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

		public Builder(ILogger logger, IPLangFileSystem fileSystem, ISettings settings, IGoalBuilder goalBuilder,
			IEventBuilder eventBuilder, IEventRuntime eventRuntime,
			PrParser prParser, IErrorHandlerFactory exceptionHandlerFactory)
		{

			this.fileSystem = fileSystem;
			this.logger = logger;
			this.settings = settings;
			this.goalBuilder = goalBuilder;
			this.eventBuilder = eventBuilder;
			this.eventRuntime = eventRuntime;
			this.prParser = prParser;
			this.exceptionHandlerFactory = exceptionHandlerFactory;
		}


		public async Task<IError?> Start(IServiceContainer container)
		{
			try
			{

				Stopwatch stopwatch = Stopwatch.StartNew();
				AppContext.SetSwitch("Builder", true);

				SetupBuildValidation();

				var goalFiles = GoalHelper.GetGoalFilesToBuild(fileSystem, fileSystem.GoalsPath);

				InitFolders();
				logger.LogInformation("Build Start:" + DateTime.Now.ToLongTimeString());

				(var eventGoalFiles, var error) = await eventBuilder.BuildEventsPr();
				if (error != null) return error;

				error = await eventRuntime.Load(true);
				if (error != null) return error;

				var eventError = await eventRuntime.RunStartEndEvents(new PLangAppContext(), EventType.Before, EventScope.StartOfApp, true);
				if (eventError != null && !eventError.IgnoreError)
				{
					return eventError;
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

				goalFiles.AddRange(eventGoalFiles);
				CleanGoalFiles(goalFiles);

				eventError = await eventRuntime.RunStartEndEvents(new PLangAppContext(), EventType.After, EventScope.EndOfApp, true);
				if (eventError != null && !eventError.IgnoreError)
				{
					return eventError;
				}
				else if (eventError != null)
				{
					logger.LogWarning(eventError.ToFormat().ToString());
				}

				ShowBuilderErrors(goalFiles, stopwatch);

				
				

			}
			catch (Exception ex)
			{
				var error = new ExceptionError(ex);
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
			var buildPath = Path.Join(fileSystem.RootDirectory, ".build");
			if (!fileSystem.Directory.Exists(buildPath))
			{
				var dir = fileSystem.Directory.CreateDirectory(buildPath);
				dir.Attributes = FileAttributes.Directory | FileAttributes.Hidden;
			}

			var dbPath = Path.Join(fileSystem.RootDirectory, ".db");
			if (!fileSystem.Directory.Exists(dbPath))
			{
				var dir = fileSystem.Directory.CreateDirectory(dbPath);
				dir.Attributes = FileAttributes.Directory | FileAttributes.Hidden;
			}
		}

		private void CleanGoalFiles(List<string> goalFiles)
		{
			var dirs = fileSystem.Directory.GetDirectories(".build", "", SearchOption.AllDirectories);

			foreach (var goalFile in goalFiles)
			{
				var buildFolderRelativePath = Path.Join(".build", goalFile.Replace(fileSystem.RootDirectory, "")).Replace(".goal", "");
				var buildFolderAbsolutePath = Path.Join(fileSystem.RootDirectory, buildFolderRelativePath);

				dirs = dirs.Where(p => !p.StartsWith(buildFolderAbsolutePath)).ToArray();
			}

			foreach (var dir in dirs)
			{
				var folderPath = Path.Join(fileSystem.RootDirectory, dir.Replace(fileSystem.BuildPath, ""));
				if (!fileSystem.Directory.Exists(folderPath) && fileSystem.Directory.Exists(dir))
				{
					fileSystem.Directory.Delete(dir, true);
				}
			}

			var prGoalFiles = prParser.ForceLoadAllGoals();
			int i = 0;

			/*

			var prGoalFiles = prParser.ForceLoadAllGoals();
			foreach (var dir in dirs)
			{
				var matchingGoal = prGoalFiles.FirstOrDefault(p => p.AbsolutePrFolderPath.ToLower().StartsWith(dir.ToLower()));
				if (matchingGoal == null && fileSystem.Directory.Exists(dir))
				{
					fileSystem.Directory.Delete(dir, true);
				}

				string goalFolder = dir.Replace(fileSystem.BuildPath, "");
				string goalFolderPath = Path.Join(fileSystem.RootDirectory, goalFolder);
				string goalFileName = dir.Replace(fileSystem.BuildPath, "") + ".goal";
				string goalFilePath = Path.Join(fileSystem.RootDirectory, goalFileName);

				if (!fileSystem.File.Exists(goalFilePath) && !fileSystem.Directory.Exists(goalFolderPath))
				{
					fileSystem.Directory.Delete(dir, true);
				}

			}*/
		}


		public void SetupBuildValidation()
		{
			var eventsPath = Path.Join(fileSystem.GoalsPath, "events", "external", "plang", "builder");

			if (fileSystem.Directory.Exists(eventsPath)) return;

			fileSystem.Directory.CreateDirectory(eventsPath);

			using (MemoryStream ms = new MemoryStream(InternalApps.Builder))
			using (ZipArchive archive = new ZipArchive(ms))
			{
				archive.ExtractToDirectory(fileSystem.GoalsPath, true);
			}
			return;

		}
	}


}
