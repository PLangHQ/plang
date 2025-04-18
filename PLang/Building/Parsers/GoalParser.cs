﻿using LightInject;
using Newtonsoft.Json;
using PLang.Building.Model;
using PLang.Container;
using PLang.Exceptions;
using PLang.Interfaces;
using PLang.Runtime;
using PLang.Utils;
using Sprache;
using System.Text.RegularExpressions;

namespace PLang.Building.Parsers
{
	public interface IGoalParser
	{
		List<Goal> GetAllGoals();
		List<Goal> ParseGoalFile(string goalFileAbsolutePath);
	}

	public class GoalParser : IGoalParser
	{
		private readonly IServiceContainer container;
		private readonly IPLangFileSystem fileSystem;
		private readonly ISettings settings;

		public GoalParser(IServiceContainer container, IPLangFileSystem fileSystem, ISettings settings)
		{
			this.container = container;
			this.fileSystem = fileSystem;
			this.settings = settings;
		}

		public List<Goal> GetAllGoals()
		{
			List<Goal> goals = new List<Goal>();
			var files = fileSystem.Directory.GetFiles(fileSystem.GoalsPath, "*.goal", SearchOption.AllDirectories);
			foreach (var file in files)
			{
				if (file.Contains(Path.DirectorySeparatorChar + ".")) continue;
				goals.AddRange(ParseGoalFile(file));
			}
			return goals;
		}

		public List<Goal> ParseGoalFile(string goalFileAbsolutePath)
		{
			Goal? currentGoal = null;
			var content = fileSystem.File.ReadAllText(goalFileAbsolutePath);
			content = content.Replace("\t", "    ");

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
					for (int b=i; b < lines.Length; b++)
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
						step.RelativeGoalPath = goalFileAbsolutePath.Replace(fileSystem.GoalsPath, "");
						
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
					prFileAbsolutePath = Path.Join(GetBuildPathOfGoalFile(goalFileAbsolutePath), ISettings.GoalFileName);
					goal.Visibility = Visibility.Public;
				}
				else
				{
					prFileAbsolutePath = Path.Join(GetBuildPathOfGoalFile(goalFileAbsolutePath), goals[i].GoalName, ISettings.GoalFileName);
					goal.Visibility = Visibility.Private;

				}
				goal.GoalFileName = Path.GetFileName(goalFileAbsolutePath);
				goal.PrFileName = Path.GetFileName(prFileAbsolutePath);

				goal.AbsoluteGoalPath = goalFileAbsolutePath;
				goal.AbsoluteGoalFolderPath = Path.GetDirectoryName(goalFileAbsolutePath);
				goal.RelativeGoalPath = goalFileAbsolutePath.Replace(fileSystem.GoalsPath, "");
				goal.RelativeGoalFolderPath = Path.GetDirectoryName(goal.RelativeGoalPath);
				if (i > 0)
				{
					goals[0].SubGoals.Add(goal.GoalName);
				}

				goal.AbsolutePrFilePath = prFileAbsolutePath;
				goal.AbsolutePrFolderPath = Path.GetDirectoryName(prFileAbsolutePath);
				goal.RelativePrPath = Path.Join(".build", prFileAbsolutePath.Replace(fileSystem.BuildPath, ""));
				goal.RelativePrFolderPath = Path.GetDirectoryName(goal.RelativePrPath);

				//check if goal inside of goal is named same as base folder
				if (i == 0)
				{
					basePrFolder = Path.GetDirectoryName(goal.RelativePrFolderPath);
					basePrGoalFolder = goal.RelativePrFolderPath;
				} else
				{
					if (Path.Join(basePrFolder, goal.GoalName) == basePrGoalFolder) {
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

				if (!fileSystem.File.Exists(prFileAbsolutePath)) continue;

				var prevBuildGoal = JsonHelper.ParseFilePath<Goal>(fileSystem, prFileAbsolutePath);
				if (prevBuildGoal == null) continue;

				goal.Description = prevBuildGoal.Description;
				goal.IncomingVariablesRequired = prevBuildGoal.IncomingVariablesRequired;
				goal.DataSourceName = prevBuildGoal.DataSourceName;

				foreach (var injection in prevBuildGoal.Injections)
				{
					if (goal.Injections.FirstOrDefault(p => p.Type == injection.Type && p.Path == injection.Path) == null)
					{
						goal.Injections.Add(injection);
					}
				}

				for (int b = 0; prevBuildGoal != null && b < goals[i].GoalSteps.Count; b++)
				{
					var prevStep = prevBuildGoal.GoalSteps.FirstOrDefault(p => p.Text == goals[i].GoalSteps[b].Text && p.LineNumber == goals[i].GoalSteps[b].LineNumber);
					if (prevStep == null)
					{
						prevStep = prevBuildGoal.GoalSteps.FirstOrDefault(p => p.Text == goals[i].GoalSteps[b].Text);
					}
					goals[i].GoalSteps[b].Index = b;
					goals[i].GoalSteps[b].RelativeGoalPath = goal.RelativeGoalPath;
					if (prevStep != null)
					{
						goals[i].GoalSteps[b].Custom = prevStep.Custom;
						goals[i].GoalSteps[b].EventBinding = prevStep.EventBinding;
						goals[i].GoalSteps[b].IsEvent = prevStep.IsEvent;
						goals[i].GoalSteps[b].Generated = prevStep.Generated;

						var absolutePrStepFilePath = Path.Join(goal.AbsolutePrFolderPath, prevStep.PrFileName);
						if (!fileSystem.File.Exists(absolutePrStepFilePath)) continue;

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


		public string GetBuildPathOfGoalFile(string goalFilePath)
		{
			var path = goalFilePath.Replace(".goal", "").Replace(fileSystem.GoalsPath, "");
			if (path.StartsWith(Path.DirectorySeparatorChar)) path = path.Substring(1);
			return Path.Join(fileSystem.BuildPath, path);
		}

	}

}
