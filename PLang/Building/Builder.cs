using LightInject;
using Microsoft.Extensions.Logging;
using PLang.Building.Events;
using PLang.Building.Parsers;
using PLang.Exceptions;
using PLang.Exceptions.AskUser;
using PLang.Exceptions.Handlers;
using PLang.Interfaces;
using PLang.Utils;
using System.Diagnostics;

namespace PLang.Building
{
    public interface IBuilder
	{
		Task Start(IServiceContainer container);
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
		private readonly IExceptionHandlerFactory exceptionHandlerFactory;

		public Builder(ILogger logger, IPLangFileSystem fileSystem, ISettings settings, IGoalBuilder goalBuilder,
			IEventBuilder eventBuilder, IEventRuntime eventRuntime,
			PrParser prParser, IExceptionHandlerFactory exceptionHandlerFactory)
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


		public async Task Start(IServiceContainer container)
		{
			try
			{
				Stopwatch stopwatch = Stopwatch.StartNew();
				AppContext.SetSwitch("Builder", true);
				
				var goalFiles = GoalHelper.GetGoalFilesToBuild(fileSystem, fileSystem.GoalsPath);
				
				InitFolders();
				logger.LogInformation("Build Start:" + DateTime.Now.ToLongTimeString());

				var eventGoalFiles = await eventBuilder.BuildEventsPr();
				await eventRuntime.Load(container, true);

				foreach (string file in goalFiles)
				{
					await goalBuilder.BuildGoal(container, file);
				}

				goalFiles.AddRange(eventGoalFiles);
				CleanGoalFiles(goalFiles);

				logger.LogInformation("\n\nBuild done - Time:" + stopwatch.Elapsed.TotalSeconds.ToString("#,##.##") + " sec");
			}
			catch (StopBuilderException) { }
			catch (Exception ex)
			{
				await exceptionHandlerFactory.CreateHandler().Handle(ex, 500, "error", ex.Message);
				if (ex.Message != "FriendlyError")
				{
				//	await errorHelper.ShowFriendlyErrorMessage(ex, callBackForAskUser: async () => { await Start(container); });
				}
				
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
	}


}
