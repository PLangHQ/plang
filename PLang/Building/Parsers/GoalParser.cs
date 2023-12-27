using LightInject;
using NBitcoin;
using Newtonsoft.Json;
using PLang.Building.Model;
using PLang.Interfaces;
using PLang.Runtime;
using PLang.Services.SettingsService;
using PLang.Utils;
using Sprache;
using System.IO.Abstractions;
using System.Text.RegularExpressions;

namespace PLang.Building.Parsers
{
    public interface IGoalParser
	{
		List<Goal> ParseGoalFile(string fileName);
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
			for (int i=0;i<lines.Length;i++)
			{
				var line = lines[i];
				if (string.IsNullOrWhiteSpace(line))
				{
					whitespace = true;
					continue;
				}

				if (line.TrimStart().StartsWith("/"))
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
					step.LineNumber = i;
					step.Number = stepNr++;

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
				}

				currentGoal = goalParser.Parse(line);
				currentGoal.Comment = uncertainComment ?? goalComment;
				goalComment = null;
				uncertainComment = null;

				currentGoal.GoalSteps = new List<GoalStep>();
				goals.Add(currentGoal);
			}
			var dict = settings.GetOrDefault<Dictionary<string, DateTime>>(typeof(Engine), "SetupRunOnce", new());
			for (int i = 0; i < goals.Count; i++)
			{
				string prFileAbsolutePath;
				var goal = goals[i];

				if (i == 0)
				{
					prFileAbsolutePath = Path.Combine(GetBuildPathOfGoalFile(goalFileAbsolutePath), ISettings.GoalFileName);
					goal.Visibility = Visibility.Public;
				}
				else
				{
					prFileAbsolutePath = Path.Combine(GetBuildPathOfGoalFile(goalFileAbsolutePath), goals[i].GoalName, ISettings.GoalFileName);
					goal.Visibility = Visibility.Private;
				}
				goal.GoalFileName = Path.GetFileName(goalFileAbsolutePath);
				goal.PrFileName = Path.GetFileName(prFileAbsolutePath);

				goal.AbsoluteGoalPath = goalFileAbsolutePath;
				goal.AbsoluteGoalFolderPath = Path.GetDirectoryName(goalFileAbsolutePath);
				goal.RelativeGoalPath = goalFileAbsolutePath.Replace(settings.GoalsPath, "");
				goal.RelativeGoalFolderPath = Path.GetDirectoryName(goal.RelativeGoalPath);

				goal.AbsolutePrFilePath = prFileAbsolutePath;
				goal.AbsolutePrFolderPath = Path.GetDirectoryName(prFileAbsolutePath);
				goal.RelativePrPath = Path.Join(".build", prFileAbsolutePath.Replace(settings.BuildPath, ""));
				goal.RelativePrFolderPath = Path.GetDirectoryName(goal.RelativePrPath);
				if (injections.Count > 0)
				{
					foreach (var injection in injections)
					{
						goal.Injections.Add(new Injections(injection.Key, injection.Value, true));
					}
					
				}
				if (!fileSystem.File.Exists(prFileAbsolutePath)) continue;

				var prevBuildGoal = JsonHelper.ParseFilePath<Goal>(fileSystem, prFileAbsolutePath);
				if (prevBuildGoal == null) continue;

				goal.Injections = prevBuildGoal.Injections;
				for (int b = 0; prevBuildGoal != null && b < goals[i].GoalSteps.Count; b++)
				{
					var prevStep = prevBuildGoal.GoalSteps.FirstOrDefault(p => p.Text == goals[i].GoalSteps[b].Text);
					if (prevStep != null)
					{
						goals[i].GoalSteps[b].Custom = prevStep.Custom;
						goals[i].GoalSteps[b].Generated = prevStep.Generated; 						

						var absolutePrFilePath = Path.Join(goal.AbsolutePrFolderPath, prevStep.PrFileName);
						if (!fileSystem.File.Exists(absolutePrFilePath)) continue;

						goals[i].GoalSteps[b].PrFileName = prevStep.PrFileName;
						goals[i].GoalSteps[b].RelativePrPath = Path.Join(goal.RelativePrFolderPath, prevStep.PrFileName);
						goals[i].GoalSteps[b].AbsolutePrFilePath = absolutePrFilePath;
						goals[i].GoalSteps[b].Number = prevStep.Number;
						goals[i].GoalSteps[b].LlmQuestion = prevStep.LlmQuestion;
						
						goals[i].GoalSteps[b].Description = prevStep.Description;
						goals[i].GoalSteps[b].WaitForExecution = prevStep.WaitForExecution;
						goals[i].GoalSteps[b].ErrorHandler = prevStep.ErrorHandler;
						goals[i].GoalSteps[b].CancellationHandler = prevStep.CancellationHandler;
						goals[i].GoalSteps[b].CacheHandler = prevStep.CacheHandler;
						
						goals[i].GoalSteps[b].Execute = prevStep.Execute;
						goals[i].GoalSteps[b].Indent = prevStep.Indent;
						goals[i].GoalSteps[b].PrFileName = prevStep.PrFileName;
						goals[i].GoalSteps[b].ModuleType = prevStep.ModuleType;
						goals[i].GoalSteps[b].Name = prevStep.Name;
						goals[i].GoalSteps[b].RetryHandler = prevStep.RetryHandler;
						goals[i].GoalSteps[b].RunOnce = prevStep.RunOnce;

						if (dict.ContainsKey(goals[i].RelativePrPath))
						{
							goals[i].GoalSteps[b].Executed = dict[goals[i].RelativePrPath];
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
			var regex = new Regex(@"^@([a-z0-9]+)\s*=\s*([a-z0-9\./\\]+)", RegexOptions.IgnoreCase);
			var matches = regex.Matches(content);
			var injections = new Dictionary<string, string>();
			foreach (Match match in matches) 
			{
				var injectName = match.Groups[1].Value;
				var injectType = match.Groups[2].Value;

				((ServiceContainer) container).RegisterForPLangUserInjections(injectName, injectType, isSetup);
				content = content.Replace(match.Value, "");

				injections.Add(injectName, injectType);
			}
			return (content.Trim(), injections);
		}
		

		public string GetBuildPathOfGoalFile(string goalFilePath)
		{
			var path = goalFilePath.Replace(".goal", "").Replace(settings.GoalsPath, "");
			if (path.StartsWith(Path.DirectorySeparatorChar)) path = path.Substring(1);
			return Path.Combine(settings.BuildPath, path);
		}

	}

}
