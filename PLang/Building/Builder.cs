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


				//var engine = runtimeContainer.GetInstance<IEngine>();
				//engine.Init(runtimeContainer);

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


				logger.LogInformation("\n\nBuild done - Time:" + stopwatch.Elapsed.TotalSeconds.ToString("#,##.##") + " sec");

			}
			catch (Exception ex)
			{
				var error = new ExceptionError(ex);
				var handler = exceptionHandlerFactory.CreateHandler();
				if (!await handler.Handle(error))
				{
					await handler.ShowError(error, null);
				}

			}
			return null;
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

			var prGoalFiles = prParser.ForceLoadAllGoals();
			foreach (var dir in dirs)
			{
				var matchingGoal = prGoalFiles.FirstOrDefault(p => p.AbsolutePrFolderPath.ToLower().StartsWith(dir.ToLower()));
				if (matchingGoal == null && fileSystem.Directory.Exists(dir))
				{
					fileSystem.Directory.Delete(dir, true);
				}
			}
		}


		public void SetupBuildValidation()
		{
			var eventsPath = Path.Join(fileSystem.GoalsPath, "events");
			var checkGoalsPath = Path.Join(eventsPath, "CheckGoals.goal");

			if (fileSystem.File.Exists(checkGoalsPath)) return;

			if (!fileSystem.File.Exists(checkGoalsPath))
			{
				if (!fileSystem.Directory.Exists(eventsPath))
				{
					fileSystem.Directory.CreateDirectory(eventsPath);
				}
				else
				{
					logger.LogError("Installed build validator and may have overwritten your events/BuildEvents.goal file. Sorry about that :( Will fix in future.");
				}

				using (MemoryStream ms = new MemoryStream(InternalApps.CheckGoals))
				using (ZipArchive archive = new ZipArchive(ms))
				{
					archive.ExtractToDirectory(fileSystem.GoalsPath, true);
				}
				return;
			}
		}
	}


}
