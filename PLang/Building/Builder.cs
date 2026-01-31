using LightInject;
using Microsoft.Extensions.Logging;
using PLang.Building.Model;
using PLang.Building.Parsers;
using PLang.Errors;
using PLang.Errors.AskUser;
using PLang.Errors.Builder;
using PLang.Errors.Handlers;
using PLang.Events;
using PLang.Events.Types;
using PLang.Exceptions;
using PLang.Exceptions.AskUser;
using PLang.Interfaces;
using PLang.Runtime;
using PLang.SafeFileSystem;
using PLang.Utils;
using System.Data;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace PLang.Building
{
	public interface IBuilder
	{
		Task<List<IBuilderError>?> Start(IServiceContainer container, PLangContext context, string? absoluteGoalPath = null);
	}
	public class Builder : IBuilder
	{
		private readonly IPLangFileSystem fileSystem;
		private readonly ILogger logger;
		private readonly ISettings settings;
		private readonly IGoalBuilder goalBuilder;
		private readonly IEventBuilder eventBuilder;
		private readonly IEventRuntime eventRuntime;
		private readonly IPrParser prParser;
		private readonly IGoalParser goalParser;
		private readonly IEngine engine;
		private readonly PLangAppContext appContext;

		public Builder(ILogger logger, IPLangFileSystem fileSystem, ISettings settings, IGoalBuilder goalBuilder,
			IEventBuilder eventBuilder, IEventRuntime eventRuntime,
			IPrParser prParser,
			IGoalParser goalParser, IEngine engine, PLangAppContext appContext)
		{

			this.fileSystem = fileSystem;
			this.logger = logger;
			this.settings = settings;
			this.goalBuilder = goalBuilder;
			this.eventBuilder = eventBuilder;
			this.eventRuntime = eventRuntime;
			this.prParser = prParser;
			this.goalParser = goalParser;
			this.engine = engine;
			this.appContext = appContext;
		}


		public async Task<List<IBuilderError>?> Start(IServiceContainer container, PLangContext context, string? absoluteGoalPath = null)
		{
			IError? error;
			try
			{
				Stopwatch stopwatch = Stopwatch.StartNew();
				AppContext.SetSwitch("Builder", true);
				Goal goal = Goal.Builder;

				var engine = container.GetInstance<IEngine>();
				engine.Context.CallStack.EnterGoal(goal);
				engine.Context.CallStack.SetCurrentStep(new GoalStep() { Name = "Step", RelativeGoalPath = goal.RelativeGoalPath, Goal = goal }, 0);

				logger.LogTrace($"Loading goal files - {stopwatch.ElapsedMilliseconds}");
				var goals = goalParser.GetGoalFilesToBuild();
				logger.LogTrace($"Done loading goal files now Init folder - {stopwatch.ElapsedMilliseconds}");

				InitFolders();

				logger.LogTrace($"Done Init folder, now load event runtime - {stopwatch.ElapsedMilliseconds}");
				logger.LogInformation("Build Start:" + DateTime.Now.ToLongTimeString());

				error = eventRuntime.Load(true);
				if (error != null) return [new BuilderError(error)];

				logger.LogTrace($"Done event runtime - {stopwatch.ElapsedMilliseconds}");
				
				var setupGoals = goals.Where(p => p.IsSetup).OrderBy(p => !p.GoalName.Equals("setup", StringComparison.OrdinalIgnoreCase));
				foreach (var setupGoal in setupGoals)
				{
					logger.LogDebug($"Start setup file build on '{setupGoal.GoalName}' - {stopwatch.ElapsedMilliseconds}");
					var goalError = await goalBuilder.BuildGoal(container, setupGoal, context);
					if (goalError != null && !goalError.ContinueBuild)
					{
						return [goalError];
					}
					else if (goalError != null)
					{
						//logger.LogWarning(goalError.ToFormat().ToString());
						goalBuilder.AddToBuildErrors(goalError);
						
					}
					logger.LogDebug($"Done Setup Build on {setupGoal.GoalName} - {stopwatch.ElapsedMilliseconds}");
				}
				context.DataSource = null;

				if (absoluteGoalPath != null)
				{
					goals = goals.Where(p => p.AbsoluteGoalPath.Equals(absoluteGoalPath)).ToList();
				}
				logger.LogDebug($"Start building BuildEvents - {stopwatch.ElapsedMilliseconds}");
				error = await eventBuilder.BuildEventsPr();
				if (error != null) return [new BuilderError(error)];
				logger.LogDebug($"Done building BuildEvents - {stopwatch.ElapsedMilliseconds}");

				var (_, eventError) = await eventRuntime.RunStartEndEvents(EventType.Before, EventScope.StartOfApp, goal, true);
				if (eventError != null)
				{
					return [new BuilderError(eventError)];
				}
				
				var goalsToBuild = goals.Where(p => !p.IsSetup && !p.IsEvent);
				if (AppContext.TryGetSwitch("Validate", out bool isEnabled) && !isEnabled)
				{
					goalsToBuild = goalsToBuild.Where(p => p.HasChanged);
				}
				foreach (var goalToBuild in goalsToBuild)
				{
					Stopwatch buildGoalTime = Stopwatch.StartNew();
					logger.LogDebug($"Building goal {goalToBuild.GoalName} - {stopwatch.ElapsedMilliseconds}");
					var goalError = await goalBuilder.BuildGoal(container, goalToBuild, context);
					if (goalError != null && !goalError.ContinueBuild)
					{
						return [goalError];
					}
					else if (goalError != null)
					{
						goalBuilder.AddToBuildErrors(goalError);
					}
					/*
					foreach (var subGoalPr in goalToBuild.SubGoals)
					{
						var subgoal = goals.FirstOrDefault(p => p.RelativePrPath == subGoalPr);
						if (subgoal != null)
						{
							subgoal.AddVariables(goalToBuild.GetVariables());
						}
					}*/
					logger.LogDebug($"Done building goal {goalToBuild.GoalName} took {buildGoalTime.ElapsedMilliseconds} - Total build time: {stopwatch.ElapsedMilliseconds}");
				}

				logger.LogDebug($"Cleaning up goal files - {stopwatch.ElapsedMilliseconds}");
				CleanGoalFiles();

				(_, eventError) = await eventRuntime.RunStartEndEvents(EventType.After, EventScope.EndOfApp, goal, true);
				if (eventError != null)
				{
					return [new BuilderError(eventError)];
				}

				ReleaseDatabase();
				ShowBuilderErrors(goals, stopwatch);
				

				logger.LogDebug($"Done - Finished cleaning, db releasee and inform user - {stopwatch.ElapsedMilliseconds}");

				return goalBuilder.BuildErrors;
			}
			catch (Exception ex)
			{
				if (ex is FileAccessException fa)
				{
					var fileAccessHandler = container.GetInstance<IFileAccessHandler>();
					var engine = container.GetInstance<IEngine>();

					(var answer, error) = await AskUser.GetAnswer(engine, context, fa.Message);
					if (error != null) return [new BuilderError(error)];

					(var _, error) = await fileAccessHandler.ValidatePathResponse(fa.AppName, fa.Path, answer.ToString(), engine.FileSystem.Id);
					if (error != null) return [new BuilderError(error)];

					return await Start(container, context);

					

				}

				if (ex is MissingSettingsException mse)
				{
					var (answer, askError) = await AskUser.GetAnswer(engine, context, mse.Message);
					if (askError != null) return [new BuilderError(askError)];

					askError = await mse.InvokeCallback(answer);
					if (askError != null) return [new BuilderError(askError)];

					return await Start(container, context);
				}

				var step = (ex is BuilderStepException bse) ? bse.Step : null;
				var goal = (ex is BuilderException be) ? be.Goal : null;

				error = new ExceptionError(ex, ex.Message, goal ?? step?.Goal, step, Key: ex.GetType().FullName);
				var (_, handleError) = await eventRuntime.AppErrorEvents(error);
				// If handleError is not null, the error was not handled
				if (handleError != null)
				{
					var me = new MultipleError(error);
					me.Add(handleError);
					logger.LogError(error.ToString());
				}

			}
			return null;
		}

		private void ReleaseDatabase()
		{
			var anchors = appContext.GetOrDefault<Dictionary<string, IDbConnection>>("AnchorMemoryDb", new());
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
			
			var prGoalFiles = prParser.ForceLoadAllGoals();
			int i = 0;

		}

	}


}
