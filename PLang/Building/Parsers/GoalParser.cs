using LightInject;
using Newtonsoft.Json;
using PLang.Building.Model;
using PLang.Container;
using PLang.Exceptions;
using PLang.Interfaces;
using PLang.Runtime;
using PLang.Utils;
using Sprache;
using System.Text.RegularExpressions;
using Instruction = PLang.Building.Model.Instruction;

namespace PLang.Building.Parsers
{
	public interface IGoalParser
	{
		List<Goal> GetAllApps();
		List<Goal> GetGoals(bool force = false);
		List<Goal> GetGoalFilesToBuild(bool force = false);
		List<Goal> ParseGoalFile(string goalFileAbsolutePath, bool isOS = false);
	}

	public class GoalParser : IGoalParser
	{
		private readonly IServiceContainer container;
		private readonly IPLangFileSystem fileSystem;
		private readonly ISettings settings;
		private List<Goal> goals = null!;
		public GoalParser(IServiceContainer container, IPLangFileSystem fileSystem, ISettings settings)
		{
			this.container = container;
			this.fileSystem = fileSystem;
			this.settings = settings;

			this.goals = GetGoals();
		}

		public List<Goal> GetGoals(bool force = false)
		{
			if (!force && goals != null && goals.Count > 0) return goals;

			List<Goal> goalsLoading = new();

			var files = fileSystem.Directory.GetFiles(fileSystem.GoalsPath, "*.goal", SearchOption.AllDirectories);
			foreach (var file in files)
			{
				if (file.Contains(Path.DirectorySeparatorChar + ".")) continue;
				goalsLoading.AddRange(ParseGoalFile(file));
			}

			goals = goalsLoading;
			return goals;
		}


		public List<Goal> GetEventGoals()
		{
			return goals.Where(p => p.IsEvent).ToList();
		}

		public List<Goal> ParseGoalFile(string goalFileAbsolutePath, bool isSystem = false)
		{
			if (fileSystem.Path.GetExtension(goalFileAbsolutePath) != ".goal")
			{
				throw new Exception($"The file {goalFileAbsolutePath} is not a goal file. It should end with .goal");
			}
			Goal? currentGoal = null;
			var content = fileSystem.File.ReadAllText(goalFileAbsolutePath);
			content = content.Replace("\t", "    ");

			string rootPath = fileSystem.RootDirectory;
			string rootBuildPath = fileSystem.BuildPath;


			var appName = "";
			if (isSystem)
			{
				rootPath = fileSystem.SystemDirectory;
				rootBuildPath = fileSystem.Path.Join(fileSystem.SystemDirectory, ".build");
			}

			var appPath = $"{Path.DirectorySeparatorChar}apps{Path.DirectorySeparatorChar}";
			if (isSystem && goalFileAbsolutePath.Contains(appPath))
			{
				var replacedPath = (isSystem) ? fileSystem.SystemDirectory : fileSystem.RootDirectory;

				if (!replacedPath.Contains(appPath))
				{
					replacedPath = Path.Join(replacedPath, appPath);
				}
				var folderAndFile = goalFileAbsolutePath.Replace($"{replacedPath}", "");
				appName = folderAndFile.Substring(0, folderAndFile.IndexOf(Path.DirectorySeparatorChar));

				rootPath = fileSystem.Path.Join(replacedPath, appName);
				rootBuildPath = fileSystem.Path.Join(replacedPath, appName, ".build");

			}
			if (!Path.IsPathFullyQualified(rootPath))
			{
				throw new Exception("not qualitfied");
			}
			if (!Path.IsPathFullyQualified(rootBuildPath))
			{
				throw new Exception("not qualitfied .build");
			}
			(content, var injections) = HandleInjections(content, true);

			var stepParser = from indent in Parse.WhiteSpace.Many()
							 from dash in Parse.Char('-').Once()
							 from stepText in Parse.AnyChar.Except(Parse.LineEnd).Many().Text()
							 select new GoalStep
							 {
								 Text = stepText.Trim(),
								 Indent = indent.Count(),
							 };

			var multiLineComment = from open in Parse.String("/*").Token()
								   from commentContent in Parse.CharExcept('*').Or(Parse.Char('*').Except(Parse.Char('/'))).Many().Text()
								   from close in Parse.String("*/").Token()
								   select commentContent.Trim();

			var commentParser = from slash in Parse.Char('/')
								from commentText in Parse.AnyChar.Except(Parse.LineEnd).Many().Text()
								select commentText.Trim();

			var goalParser = from goalText in Parse.AnyChar.Except(Parse.LineEnd).Many().Text()
							 from comment in Parse.LineEnd.Then(_ => commentParser).Optional()
							 select new Goal { GoalName = goalText.Trim(), Comment = comment.GetOrDefault() };




			var lines = content.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);

			var goals = new List<Goal>();

			string? goalComment = null;
			string? stepComment = null;
			string? uncertainComment = null;
			bool whitespace = false;
			int stepNr = 1;
			for (int i = 0; i < lines.Length; i++)
			{
				var line = lines[i];
				if (string.IsNullOrWhiteSpace(line))
				{
					whitespace = true;
					continue;
				}
				if (line.TrimStart().StartsWith("/*"))
				{
					for (int b = i; b < lines.Length; b++)
					{
						uncertainComment += lines[b].Trim() + "\n";
						if (lines[b].TrimEnd().EndsWith("*/"))
						{
							i = b;
							break;
						}
					}

					continue;
				}
				else if (line.TrimStart().StartsWith("/"))
				{
					var comment = commentParser.Parse(line.Trim());
					if (whitespace)
					{
						if (!string.IsNullOrEmpty(uncertainComment)) { uncertainComment += ". "; }
						uncertainComment = comment;
					}
					if (currentGoal != null)
					{
						if (!string.IsNullOrEmpty(stepComment)) { stepComment += ". "; }
						stepComment = comment;
					}
					else if (currentGoal == null)
					{
						if (!string.IsNullOrEmpty(goalComment)) { goalComment += ". "; }
						goalComment += comment;
					}
					continue;
				}

				if (line.TrimStart().StartsWith("-"))
				{
					var step = stepParser.Parse(line);
					if (step == null) continue;

					step.Comment = uncertainComment ?? stepComment;
					step.Execute = step.Indent == 0;


					step.Goal = currentGoal;
					step.LineNumber = (i + 1);
					step.Number = stepNr++;

					if (step.Indent % 4 != 0)
					{
						step.RelativeGoalPath = goalFileAbsolutePath.Replace(rootPath, "");

						throw new BuilderStepException($"Indentation of step {step.Text} is not correct. Indentation must be a multiple of 4", step);
					}

					currentGoal?.GoalSteps.Add(step);
					stepComment = null;
					uncertainComment = null;
					continue;
				}

				if (line.StartsWith(" ") || line.StartsWith('\t'))
				{
					var step = currentGoal.GoalSteps.LastOrDefault();
					if (step != null)
					{
						step.Text += '\n' + line;
						continue;
					}
					else
					{
						currentGoal.Text += ('\n' + line).TrimEnd();
					}
				}

				currentGoal = goalParser.Parse(line);
				currentGoal.Comment = uncertainComment ?? goalComment;
				currentGoal.Text = line;
				goalComment = null;
				uncertainComment = null;
				stepNr = 0;
				currentGoal.GoalSteps = new List<GoalStep>();

				goals.Add(currentGoal);
				if (goals.Count > 1)
				{
					currentGoal.ParentGoal = goals[0];
				}
			}

			if (goals.Count() == 0)
			{
				throw new Exception($"No goal defined in {goalFileAbsolutePath}. Are you missing a goal name in the goal file?");
			}

			var setupOnceDictionary = settings.GetOrDefault<Dictionary<string, DateTime>>(typeof(Engine), "SetupRunOnce", new());
			var goalsWithSameName = goals.GroupBy(p => p.GoalName).Where(p => p.Count() > 1).FirstOrDefault();
			if (goalsWithSameName != null)
			{
				var goalWithSameName = goalsWithSameName.FirstOrDefault();
				throw new BuilderException($"Goal '{goalWithSameName.GoalName}' is defined two times in {goalFileAbsolutePath}. Each goal must have unique name");
			}
			var basePrFolder = "";
			var basePrGoalFolder = "";

			for (int i = 0; i < goals.Count; i++)
			{
				string prFileAbsolutePath;
				var goal = goals[i];

				if (i == 0)
				{
					prFileAbsolutePath = Path.Join(GetBuildPathOfGoalFile(goalFileAbsolutePath, rootPath, rootBuildPath), ISettings.GoalFileName);
					goal.Visibility = Visibility.Public;
				}
				else
				{
					prFileAbsolutePath = Path.Join(GetBuildPathOfGoalFile(goalFileAbsolutePath, rootPath, rootBuildPath), goals[i].GoalName, ISettings.GoalFileName);
					goal.Visibility = Visibility.Private;

				}
				goal.AppName = "/apps/" + appName;
				goal.AbsoluteAppStartupFolderPath = rootPath;

				goal.GoalFileName = Path.GetFileName(goalFileAbsolutePath);
				goal.PrFileName = Path.GetFileName(prFileAbsolutePath);
				goal.FileHash = content.ComputeHash().Hash;
				goal.AbsoluteGoalPath = goalFileAbsolutePath;
				goal.AbsoluteGoalFolderPath = Path.GetDirectoryName(goalFileAbsolutePath);
				goal.RelativeGoalPath = goalFileAbsolutePath.Replace(rootPath, "");
				goal.RelativeGoalFolderPath = Path.GetDirectoryName(goal.RelativeGoalPath);

				goal.AbsolutePrFilePath = prFileAbsolutePath;
				goal.AbsolutePrFolderPath = Path.GetDirectoryName(prFileAbsolutePath);
				goal.RelativePrPath = Path.Join(".build", prFileAbsolutePath.Replace(rootBuildPath, ""));
				goal.RelativePrFolderPath = Path.GetDirectoryName(goal.RelativePrPath);

				if (i > 0)
				{
					goals[0].SubGoals.Add(goal.RelativePrPath);
				}

				if (!goal.AbsolutePrFilePath.StartsWith("c:") && goal.AbsolutePrFilePath.Contains("c:"))
				{
					throw new Exception($"Absolute path contains c in wrong place: {goal.AbsolutePrFilePath}");
				}

				if (goal.RelativeGoalPath.Contains("c:"))
				{
					throw new Exception($"Relative path contains full path: {goal.RelativeGoalPath}");
				}
				//check if goal inside of goal is named same as base folder
				if (i == 0)
				{
					basePrFolder = Path.GetDirectoryName(goal.RelativePrFolderPath);
					basePrGoalFolder = goal.RelativePrFolderPath;
				}
				else
				{
					if (Path.Join(basePrFolder, goal.GoalName) == basePrGoalFolder)
					{
						throw new Exception($"The goal {goal.GoalName} is named the same the the goal file {goal.GoalFileName}. This is not allowed");
					}
				}
				if (injections.Count > 0)
				{
					foreach (var injection in injections)
					{
						var inj = new Injections(injection.Key, injection.Value, true, null, null);
						inj.AtSignInjection = true;
						goal.Injections.Add(inj);
					}

				}

				goal.IsSetup = GoalHelper.IsSetup(goal);
				goal.IsEvent = GoalHelper.IsEvent(goal);

				var prevBuildGoal = JsonHelper.ParseFilePath<Goal>(fileSystem, prFileAbsolutePath);
				if (prevBuildGoal == null) continue;

				goal.Description = prevBuildGoal.Description;
				goal.IncomingVariablesRequired = prevBuildGoal.IncomingVariablesRequired;
				goal.DataSourceName = prevBuildGoal.DataSourceName;
				goal.IsSystem = isSystem;
				goal.HasChanged = prevBuildGoal.FileHash != goal.FileHash;
				foreach (var injection in prevBuildGoal.Injections)
				{
					if (goal.Injections.FirstOrDefault(p => p.Type == injection.Type && p.Path == injection.Path) == null)
					{
						goal.Injections.Add(injection);
					}
				}

				for (int b = 0; b < goals[i].GoalSteps.Count; b++)
				{
					goals[i].GoalSteps[b].Index = b;
					goals[i].GoalSteps[b].Number = b+1;
					goals[i].GoalSteps[b].RelativeGoalPath = goal.RelativeGoalPath;
					goals[i].GoalSteps[b].IsEvent = goal.IsEvent;

					if (prevBuildGoal == null) continue;

					var prevStep = prevBuildGoal.GoalSteps.FirstOrDefault(p => p.Text == goals[i].GoalSteps[b].Text && p.LineNumber == goals[i].GoalSteps[b].LineNumber);
					if (prevStep == null)
					{
						prevStep = prevBuildGoal.GoalSteps.FirstOrDefault(p => p.Text == goals[i].GoalSteps[b].Text);
						if (prevStep == null) continue;
					}


					goals[i].GoalSteps[b].EventBinding = prevStep.EventBinding;
					goals[i].GoalSteps[b].IsEvent = goal.IsEvent;
					goals[i].GoalSteps[b].Generated = prevStep.Generated;

					var absolutePrStepFilePath = fileSystem.Path.Join(goal.AbsolutePrFolderPath, prevStep.PrFileName);
					var instruction = JsonHelper.ParseFilePath<Instruction>(fileSystem, absolutePrStepFilePath);
					
					if (instruction == null) continue;

					// todo: this HasChange is not good enough, function might have change
					// should validate hash of file and the signature also.
					// it should allow modifying of function just give warning.
					goals[i].GoalSteps[b].HasChanged = !prevStep.Text.Equals(instruction.Text);
					goals[i].GoalSteps[b].PrFileName = prevStep.PrFileName;
					goals[i].GoalSteps[b].RelativePrPath = Path.Join(goal.RelativePrFolderPath, prevStep.PrFileName);
					goals[i].GoalSteps[b].AbsolutePrFilePath = absolutePrStepFilePath;
					goals[i].GoalSteps[b].Number = prevStep.Number;
					goals[i].GoalSteps[b].LlmRequest = prevStep.LlmRequest;

					goals[i].GoalSteps[b].Description = prevStep.Description;
					goals[i].GoalSteps[b].WaitForExecution = prevStep.WaitForExecution;
					goals[i].GoalSteps[b].ErrorHandlers = prevStep.ErrorHandlers;
					goals[i].GoalSteps[b].CancellationHandler = prevStep.CancellationHandler;
					goals[i].GoalSteps[b].CacheHandler = prevStep.CacheHandler;

					goals[i].GoalSteps[b].PrFileName = prevStep.PrFileName;
					goals[i].GoalSteps[b].ModuleType = prevStep.ModuleType;
					goals[i].GoalSteps[b].Name = prevStep.Name;
					goals[i].GoalSteps[b].UserIntent = prevStep.UserIntent;
					goals[i].GoalSteps[b].RunOnce = prevStep.RunOnce;

					var prFile = fileSystem.File.ReadAllText(absolutePrStepFilePath);
					goals[i].GoalSteps[b].PrFile = JsonConvert.DeserializeObject(prFile);


					if (setupOnceDictionary != null && goals[i].GoalSteps[b].RunOnce && setupOnceDictionary.ContainsKey(goals[i].GoalSteps[b].RelativePrPath))
					{
						goals[i].GoalSteps[b].Executed = setupOnceDictionary[goals[i].GoalSteps[b].RelativePrPath];
					}

					if (prevStep.Text.Trim() != goals[i].GoalSteps[b].Text.Trim())
					{
						goals[i].GoalSteps[b].PreviousText = prevStep.Text;
					}


					prevStep = prevBuildGoal.GoalSteps.FirstOrDefault(p => goals[i].GoalSteps[b].Text.Trim().StartsWith("/") && p.Text == goals[i].GoalSteps[b].Text.Trim().TrimStart('/'));
					if (prevStep != null)
					{
						goals[i].GoalSteps[b].Execute = false;
					}
				}
			}


			return goals;
		}

		private (string content, Dictionary<string, string> injections) HandleInjections(string content, bool isSetup)
		{
			var regex = new Regex(@"^@([a-z0-9]+)\s*=(.*)$", RegexOptions.IgnoreCase | RegexOptions.Multiline);
			var matches = regex.Matches(content);
			var injections = new Dictionary<string, string>();
			foreach (Match match in matches)
			{
				var injectName = match.Groups[1].Value.Trim();
				var injectType = match.Groups[2].Value.Trim();

				((ServiceContainer)container).RegisterForPLangUserInjections(injectName, injectType, isSetup);
				content = content.Replace(match.Value, "");

				injections.Add(injectName, injectType);
			}
			return (content.Trim(), injections);
		}


		public string GetBuildPathOfGoalFile(string goalFilePath, string rootPath, string rootBuildPath)
		{
			var path = goalFilePath.Replace(".goal", "").Replace(rootPath, "");
			if (path.StartsWith(Path.DirectorySeparatorChar)) path = path.Substring(1);
			return Path.Join(rootBuildPath, path);
		}

		public List<Goal> GetAllApps()
		{
			var appFolders = fileSystem.Directory.Exists("/apps") ? fileSystem.Directory.GetDirectories("/apps") : [];

			var osPath = fileSystem.Path.Join(fileSystem.SystemDirectory, "/apps");
			var osAppFolders = fileSystem.Directory.Exists(osPath) ? fileSystem.Directory.GetDirectories(osPath) : [];

			List<Goal> apps = new List<Goal>();
			foreach (var folder in appFolders)
			{
				var files = fileSystem.Directory.GetFiles(folder, "*.goal", SearchOption.AllDirectories);
				foreach (var file in files)
				{
					if (file.Contains(Path.DirectorySeparatorChar + ".")) continue;
					apps.AddRange(ParseGoalFile(file));
				}
			}

			foreach (var folder in osAppFolders)
			{
				var files = fileSystem.Directory.GetFiles(folder, "*.goal", SearchOption.AllDirectories);
				foreach (var file in files)
				{
					if (file.Contains(Path.DirectorySeparatorChar + ".")) continue;
					var goals = ParseGoalFile(file, true);


					apps.AddRange(goals);
				}
			}


			return apps;
		}

		public List<Goal> GetGoalFilesToBuild(bool force = false)
		{
			var goals = GetGoals(force);
			var goalsToBuild = goals.Where(p => !p.IsEvent);

			var orderedFiles = goalsToBuild
				.OrderBy(goal => !goal.RelativeGoalFolderPath.Equals(Path.DirectorySeparatorChar.ToString()))
				.ThenBy(goal => goal.RelativeGoalPath)
				.ThenBy(goal => goal.Visibility != Visibility.Public)
				.ToList();


			return orderedFiles;
			/*

			string[] anyFile = fileSystem.Directory.GetFiles(goalsPath, "*.goal", SearchOption.AllDirectories);
			if (anyFile.Length == 0)
			{
				throw new BuilderException($"No goal files found in directory. Are you in the correct directory? I am running from {goalsPath}");
			}
			var goalFiles = fileSystem.Directory.GetFiles(goalsPath, "*.goal", SearchOption.AllDirectories).ToList();
			var files = Remove_SystemFolder(goalsPath, goalFiles);
			List<Goal> goals = new();
			foreach (var file in files)
			{
				goals.AddRange(ParseGoalFile(file));
			}
			return goals;*/

		}
		/*
		private static List<string> Remove_SystemFolder(string goalPath, List<string> goalFiles)
		{


			string[] dirsToExclude = new string[] { "apps", ".modules", ".services", ".build", ".deploy", ".db" };
			string[] filesToExclude = new string[] { "Events.goal", "BuilderEvents.goal" };


			// Filter out excluded directories and files first to simplify subsequent operations
			var filteredGoalFiles = goalFiles.Where(goalFile =>
			{
				var relativePath = goalFile.Replace(goalPath, "").TrimStart(Path.DirectorySeparatorChar);
				var baseFolderName = Path.GetDirectoryName(relativePath).Split(Path.DirectorySeparatorChar).FirstOrDefault();
				var fileName = Path.GetFileName(goalFile).ToLower();

				return !baseFolderName.StartsWith(".") && !dirsToExclude.Contains(baseFolderName, StringComparer.OrdinalIgnoreCase) && !filesToExclude.Contains(fileName, StringComparer.OrdinalIgnoreCase);
			}).ToList();

			// Order the files
			;


			return orderedFiles;
		}*/
	}

}
