using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using PLang.Attributes;
using PLang.Modules;
using PLang.Runtime;
using System;
using System.Diagnostics;
using System.IO.Abstractions;
using System.Runtime.Serialization;
using static PLang.Modules.BaseBuilder;

namespace PLang.Building.Model
{
	public enum Visibility
	{
		Private = 0, Public = 1
	}


	public class Goal : VariableContainer
	{
		public Goal()
		{
			GoalSteps = new List<GoalStep>();
			Injections = new();
			SubGoals = new();
		}

		public Goal(string goalName, List<GoalStep> steps)
		{
			this.GoalName = goalName;
			this.GoalSteps = steps;
			GoalSteps = new List<GoalStep>();
			Injections = new();
			SubGoals = new();
		}

		public string GoalName { get; set; }
		public string? Comment { get; set; }
		public string Text { get; set; }
		public List<GoalStep> GoalSteps { get; set; }
		public List<string> SubGoals { get; set; }
		public string? Description { get; set; }
		[LlmIgnore]
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
		[LlmIgnore]
		public string RelativeGoalPath { get; set; }
		[LlmIgnore]
		public string RelativeGoalFolderPath { get; set; }
		[LlmIgnore]
		public string RelativePrPath { get; set; }
		
		[LlmIgnore]
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
		[LlmIgnore]
		public string BuilderVersion { get; set; }
		public GoalInfo GoalInfo { get; set; }

		[LlmIgnore]
		public List<Injections> Injections { get; set; }

		[LlmIgnore]
		//Signature should be used when goal is deployed
		//this allows for validating the publisher and that code has not changed.
		public string Signature { get; set; }
		
		[Newtonsoft.Json.JsonIgnore]
		[IgnoreDataMemberAttribute]
		[System.Text.Json.Serialization.JsonIgnore]
		public bool HasChanged { get; set; }
		[LlmIgnore]
		public string FileHash { get; set; }
		[LlmIgnore]
		public string Hash { get; set; }

		[Newtonsoft.Json.JsonIgnore]
		[IgnoreDataMemberAttribute]
		[System.Text.Json.Serialization.JsonIgnore]
		public Goal? ParentGoal { get; set; }

		[IgnoreWhenInstructed]
		[LlmIgnore]
		public bool IsOS { get; set; }

		[IgnoreWhenInstructed]
		[LlmIgnore]
		public int CurrentStepIndex { get; set; }
		public string GetGoalAsString()
		{
			string goal = "";
			if (!string.IsNullOrWhiteSpace(Comment)) goal = $"/ {this.Comment}\n";
			goal += this.GoalName + "\n";
			foreach (var step in GoalSteps)
			{
				if (!string.IsNullOrWhiteSpace(step.Comment)) goal += $"/ {step.Comment}\n";
				goal += "- ".PadLeft(step.Indent, ' ') + step.Text + "\n";
			}
			return goal;
		}

		protected override Goal? GetParent()
		{
			return ParentGoal;
		}

		public Dictionary<string, string>? IncomingVariablesRequired { get; set; }
		[LlmIgnore]
		public string? DataSourceName { get; set; }
		[LlmIgnore]
		public bool IsSetup { get; set; }

		[LlmIgnore]
		[IgnoreWhenInstructed]
		public Stopwatch Stopwatch { get; set; }


		
	}

	

}
