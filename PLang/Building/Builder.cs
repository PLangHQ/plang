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
		Task<List<IBuilderError>?> Start(IServiceContainer container, PLangContext context, IReadOnlyCollection<string>? absoluteGoalPaths = null, bool withSetupGoals = false);
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
		private readonly IGoalParser goalParser;
		private readonly IEngine engine;
		private readonly PLangAppContext appContext;

		public Builder(ILogger logger, IPLangFileSystem fileSystem, ISettings settings, IGoalBuilder goalBuilder,
			IEventBuilder eventBuilder, IEventRuntime eventRuntime,
			PrParser prParser, IErrorHandlerFactory exceptionHandlerFactory, 
			IGoalParser goalParser, IEngine engine, PLangAppContext appContext)
		{

			this.fileSystem = fileSystem;
			this.logger = logger;
			this.settings = settings;
			this.goalBuilder = goalBuilder;
			this.eventBuilder = eventBuilder;
			this.eventRuntime = eventRuntime;
			this.prParser = prParser;
			this.exceptionHandlerFactory = exceptionHandlerFactory;
			this.goalParser = goalParser;
			this.engine = engine;
			this.appContext = appContext;
		}


		public async Task<List<IBuilderError>?> Start(IServiceContainer container, PLangContext context, IReadOnlyCollection<string>? absoluteGoalPaths = null, bool withSetupGoals = false)
		{
			IError? error;
			// The switch is process wide and half the runtime reads it: BaseProgram sets IsBuilder from
			// it, and DbModule then resolves every sqlite datasource to an empty in memory copy. From the
			// cli the process ends with the build, but a build run from inside a running app left the
			// switch on, and every request after it failed with "no such table". Put it back on exit.
			AppContext.TryGetSwitch("Builder", out bool wasBuilder);
			try
			{
				Stopwatch stopwatch = Stopwatch.StartNew();
				AppContext.SetSwitch("Builder", true);
				Goal goal = Goal.Builder;

				var engine = container.GetInstance<IEngine>();
				engine.Context.CallStack.EnterGoal(goal);
				engine.Context.CallStack.SetCurrentStep(new GoalStep() { Name = "Step", RelativeGoalPath = goal.RelativeGoalPath, Goal = goal }, 0);

				logger.LogTrace($"Loading goal files - {stopwatch.ElapsedMilliseconds}");
				// GoalParser caches the parsed goal files for the life of the process. The cli builds in a
				// fresh process so the cache is always current, but a build asked for from inside a running
				// app (PlangModule.BuildPlangCode) sees the list as it was at startup: a goal file written
				// since then is missing, the absoluteGoalPath filter below then matches nothing, and the
				// build returns no errors while having built nothing. Naming a goal path means a targeted
				// rebuild of what is on disk now, so reload for that case.
				bool targeted = absoluteGoalPaths != null && absoluteGoalPaths.Count > 0;
				var goals = goalParser.GetGoalFilesToBuild(force: targeted);
				logger.LogTrace($"Done loading goal files now Init folder - {stopwatch.ElapsedMilliseconds}");

				InitFolders();

				logger.LogTrace($"Done Init folder, now load event runtime - {stopwatch.ElapsedMilliseconds}");
				logger.LogInformation("Build Start:" + DateTime.Now.ToLongTimeString());

				error = eventRuntime.Load(true);
				if (error != null) return [new BuilderError(error)];

				logger.LogTrace($"Done event runtime - {stopwatch.ElapsedMilliseconds}");

				// Naming a goal path means build that file and nothing else. The filter used to sit after
				// the setup loop, so a targeted build still rebuilt every setup goal: slow, and inside a
				// running app it fails, because setup sql is validated against an anchor db that is only
				// populated by the create-table steps of that same build.
				//
				// That anchor db is exactly why a cli build keeps the setup goals: a build resolves a
				// sqlite datasource to an empty in-memory database, so a step selecting from a table
				// only validates when the create-table step of a Setup/ goal has run in this same
				// build. Skipping them there made every goal holding sql unbuildable. A build asked
				// for from inside a running app (withSetupGoals: false) keeps the narrow behaviour.
				if (targeted)
				{
					var wanted = new HashSet<string>(absoluteGoalPaths!, StringComparer.OrdinalIgnoreCase);
					goals = goals.Where(p => wanted.Contains(p.AbsoluteGoalPath) || (withSetupGoals && p.IsSetup)).ToList();
				}

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
				// Goals do not feed each other either, so with --buildparallel they can go out together.
				// Two exceptions stay sequential and are not a locking problem but a scoping one:
				// RegisterForPLangUserInjections writes into the DI container, which is process wide
				// while an injection is meant to apply to one goal, and the memory stack is shared.
				// A goal that mentions inject is therefore never run beside another one. In this app
				// that is 1 goal file out of 636.
				var degreeOfParallelism = AppContext.GetData("buildparallel") as int? ?? RegisterStartupParameters.DefaultBuildParallel;
				var goalList = goalsToBuild.ToList();
				var mayShareState = goalList.Where(g => g.GoalSteps.Any(s =>
					s.Text.Contains("inject", StringComparison.OrdinalIgnoreCase))).ToList();
				var standalone = goalList.Where(g => !mayShareState.Contains(g)).ToList();

				if (degreeOfParallelism > 1 && standalone.Count > 1)
				{
					var goalErrors = new System.Collections.Concurrent.ConcurrentBag<IBuilderError>();
					using (var gate = new SemaphoreSlim(degreeOfParallelism))
					{
						await Task.WhenAll(standalone.Select(async goalToBuild =>
						{
							await gate.WaitAsync();
							try
							{
								var err = await goalBuilder.BuildGoal(container, goalToBuild, context);
								if (err != null) goalErrors.Add(err);
							}
							finally { gate.Release(); }
						}));
					}
					foreach (var err in goalErrors)
					{
						if (!err.ContinueBuild) return [err];
						goalBuilder.AddToBuildErrors(err);
					}
					goalsToBuild = mayShareState;
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

				// The orphan sweep is a whole repo operation: it deletes the .pr folder of every goal
				// whose .goal file is gone. A targeted build was asked about one file and knows nothing
				// about the rest, and it typically runs inside a live app, where sweeping on a stale
				// view of the repo deletes build output the app is still serving from.
				if (!targeted)
				{
					logger.LogDebug($"Cleaning up goal files - {stopwatch.ElapsedMilliseconds}");
					CleanGoalFiles();
				}

				(_, eventError) = await eventRuntime.RunStartEndEvents(EventType.After, EventScope.EndOfApp, goal, true);
				if (eventError != null)
				{
					return [new BuilderError(eventError)];
				}

				ReleaseDatabase();
				AddErrorsForStepsWithoutPr(goals);
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
			finally
			{
				AppContext.SetSwitch("Builder", wasBuilder);
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

		// A step can end up without a .pr without anything being added to BuildErrors - the build is
		// abandoned part way through a goal, and what is left on disk is a Goal.pr whose steps point at
		// files that were never written. Nothing complained, and the goal only broke when it was run.
		// So the state on disk is checked directly rather than trusting that every failure reported itself.
		private void AddErrorsForStepsWithoutPr(List<Goal> goals)
		{
			foreach (var goal in goals)
			{
				foreach (var step in goal.GoalSteps)
				{
					if (!string.IsNullOrEmpty(step.PrFileName)) continue;
					if (goalBuilder.BuildErrors.Any(p => p.Step == step)) continue;

					goalBuilder.AddToBuildErrors(new BuilderError(
						"Step has no .pr file - it was never built. Run the build again; if it keeps happening the step text is what the builder could not map to a module.",
						Key: "StepNotBuilt")
					{
						Step = step,
						Goal = goal
					});
				}
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

				// The detail above scrolls away in a build of any size, and the exit code used to be 0
				// either way - so a step that never built looked exactly like a clean build until it
				// failed at runtime with "Instruction file could not be loaded". This summary is the
				// last thing printed: which step, in which file, and why.
				var summary = new System.Text.StringBuilder();
				summary.AppendLine($"\n\n❌ BUILD FAILED - {goalBuilder.BuildErrors.Count} step(s) did not build");
				summary.AppendLine("   These steps have no .pr file and WILL fail at runtime.\n");

				foreach (var buildError in goalBuilder.BuildErrors)
				{
					var step = buildError.Step;
					var where = step != null
						? $"{step.RelativeGoalPath}:{step.LineNumber}"
						: buildError.Goal?.RelativeGoalPath ?? "(unknown goal)";

					summary.AppendLine($"   {where}");
					if (step != null && !string.IsNullOrEmpty(step.Text))
					{
						var stepText = step.Text.ReplaceLineEndings(" ").Trim();
						if (stepText.Length > 120) stepText = stepText.Substring(0, 120) + "...";
						summary.AppendLine($"       {stepText}");
					}
					summary.AppendLine($"       → {buildError.Message?.ReplaceLineEndings(" ").Trim()}\n");
				}

				logger.LogError(summary.ToString());
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
				// ForceLoadAllGoals returns the runtime's own system goals as well as the app's.
				// Those live under SystemDirectory, not under the app being built, so their .goal
				// file is never found here and the orphan sweep below deleted them: building any
				// app wiped the installed runtime's event handlers, OnAppError and the rest.
				if (goal.IsSystem) continue;

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
