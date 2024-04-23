using LightInject;
using Microsoft.Extensions.Logging;
using PLang.Building.Model;
using PLang.Building.Parsers;
using PLang.Container;
using PLang.Events;
using PLang.Exceptions;
using PLang.Exceptions.AskUser;
using PLang.Exceptions.Handlers;
using PLang.Interfaces;
using PLang.Runtime;
using PLang.Services.OutputStream;
using PLang.Services.SigningService;
using PLang.Utils;
using System;
using System.Diagnostics;
using System.Reflection;
using System.Text;

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
				

				var runtimeContainer = new ServiceContainer();
				runtimeContainer.RegisterForPLang(fileSystem.RootDirectory, fileSystem.RelativeAppPath, container.GetInstance<IAskUserHandlerFactory>(),
					container.GetInstance<IOutputStreamFactory>(), exceptionHandlerFactory);



				var engine = runtimeContainer.GetInstance<IEngine>();
				engine.Init(runtimeContainer);
				var eventRuntime = runtimeContainer.GetInstance<IEventRuntime>();

				await eventRuntime.Load(runtimeContainer, true);
				await eventRuntime.RunStartEndEvents(new PLangAppContext(), EventType.Before, EventScope.StartOfApp, false);
				foreach (string file in goalFiles)
				{
					await goalBuilder.BuildGoal(container, file);
				}

				goalFiles.AddRange(eventGoalFiles);
				CleanGoalFiles(goalFiles);

				await eventRuntime.RunStartEndEvents(new PLangAppContext(), EventType.After, EventScope.EndOfApp, false);
				
				logger.LogInformation("\n\nBuild done - Time:" + stopwatch.Elapsed.TotalSeconds.ToString("#,##.##") + " sec");
			}
			catch (StopBuilderException) { }
			catch (Exception ex)
			{
				var handler = exceptionHandlerFactory.CreateHandler();
				if (!await handler.Handle(ex, 500, "error", ex.Message))
				{
					await handler.ShowError(ex, 500, "error", ex.Message, null);
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
