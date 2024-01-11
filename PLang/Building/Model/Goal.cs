using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using PLang.Modules;
using System.IO.Abstractions;
using System.Runtime.Serialization;
using static PLang.Modules.BaseBuilder;

namespace PLang.Building.Model
{
	/*
	public class GoalFile
	{
		public GoalFile()
		{
		}
		public GoalFile(string fileName)
		{
			this.FileName = fileName;
			FileNameWithoutExtension = Path.GetFileNameWithoutExtension(fileName);
			Goals = new List<Goal>();
		}

		public string FileName { get; set; }
		public string FileNameWithoutExtension { get; set; }
		public List<Goal> Goals { get; set; }
		public string Assistant { get; set; }

		
	}*/

	public enum Visibility
	{
		Private = 0, Public = 1
	}
	public class Goal
	{
		public Goal()
		{
			GoalSteps = new List<GoalStep>();
			Injections = new();
		}

		public Goal(string goalName, List<GoalStep> steps)
		{
			this.GoalName = goalName;
			this.GoalSteps = steps;
			GoalSteps = new List<GoalStep>();
			Injections = new();
		}

		public string GoalName { get; set; }
		public string? Comment { get; set; }
		public List<GoalStep> GoalSteps { get; set; }
		public string? Description { get; internal set; }
		public Visibility Visibility { get; set; }
		[Newtonsoft.Json.JsonIgnore]
		[IgnoreDataMemberAttribute]

		[System.Text.Json.Serialization.JsonIgnore]
		public string AppName { get; set; }
		public string GoalFileName { get; set; }
		[Newtonsoft.Json.JsonIgnore]
		[IgnoreDataMemberAttribute]

		[System.Text.Json.Serialization.JsonIgnore]
		public string PrFileName { get; set; }
		public string RelativeGoalPath { get; set; }
		public string RelativeGoalFolderPath { get; set; }
		public string RelativePrPath { get; set; }
		public string RelativePrFolderPath { get; set; }
		[Newtonsoft.Json.JsonIgnore]
		[IgnoreDataMemberAttribute]
		[System.Text.Json.Serialization.JsonIgnore]
		public string AbsoluteGoalPath { get; set; }
		[Newtonsoft.Json.JsonIgnore]
		[IgnoreDataMemberAttribute]

		[System.Text.Json.Serialization.JsonIgnore]
		public string AbsoluteGoalFolderPath { get; set; }
		[Newtonsoft.Json.JsonIgnore]
		[IgnoreDataMemberAttribute]

		[System.Text.Json.Serialization.JsonIgnore]
		public string AbsolutePrFilePath { get; set; }
		[Newtonsoft.Json.JsonIgnore]
		[IgnoreDataMemberAttribute]

		[System.Text.Json.Serialization.JsonIgnore]
		public string AbsolutePrFolderPath { get; set; }
		[Newtonsoft.Json.JsonIgnore]
		[IgnoreDataMemberAttribute]

		[System.Text.Json.Serialization.JsonIgnore]
		public string AbsoluteAppStartupFolderPath { get; set; }
		[Newtonsoft.Json.JsonIgnore]
		[IgnoreDataMemberAttribute]

		[System.Text.Json.Serialization.JsonIgnore]
		public string RelativeAppStartupFolderPath { get; set; }
		public string BuilderVersion { get; set; }
		public GoalApiInfo? GoalApiInfo { get; set; }

		public List<Injections> Injections { get; set; }

		//Signature should be used when goal is deployed
		//this allows for validating the publisher and that code has not changed.
		public string Signature { get; set; }
		public string Hash { get; set; }
		public string GetGoalAsString()
		{
			string goal = this.GoalName;
			foreach (var step in GoalSteps)
			{
				goal += "- ".PadLeft(step.Indent, ' ') + step.Text + "\n";
			}
			return goal;
		}



	}



}
