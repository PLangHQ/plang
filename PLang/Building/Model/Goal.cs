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

		[LlmIgnore]
		public string PrFileName { get; set; }
		[LlmIgnore]
		public string RelativeGoalPath { get; set; }
		[LlmIgnore]
		public string RelativeGoalFolderPath { get; set; }
		[LlmIgnore]
		public string RelativePrPath { get; set; }
		
		[LlmIgnore]
		public string RelativePrFolderPath { get; set; }
		
		[LlmIgnore]
		public string AbsoluteGoalPath { get; set; }

		[IgnoreWhenInstructed]
		public string AbsoluteGoalFolderPath { get; set; }

		[LlmIgnore]
		public string AbsolutePrFilePath { get; set; }

		[LlmIgnore]
		public string AbsolutePrFolderPath { get; set; }

		[LlmIgnore]
		public string AbsoluteAppStartupFolderPath { get; set; }

		[LlmIgnore]
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
		public string Hash { get; set; }

		private Goal? parentGoal;
		[Newtonsoft.Json.JsonIgnore]
		[IgnoreDataMemberAttribute]
		[System.Text.Json.Serialization.JsonIgnore]
		public Goal? ParentGoal { 
			get
			{
				return parentGoal;
			}
			set
			{
				if (value?.Hash != null && value?.Hash == Hash) return;
				parentGoal = value;
			}
		}

		[IgnoreWhenInstructed]
		[LlmIgnore]
		public bool IsSystem { get; set; }
		[IgnoreWhenInstructed]
		public string UniqueId { get; set; }
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
		protected override GoalStep? GetStep()
		{
			if (GoalSteps == null || GoalSteps.Count <= CurrentStepIndex) return null;	
			return GoalSteps?[CurrentStepIndex];
		}
		protected override Goal? GetParent()
		{
			var parentGoal = ParentGoal;
			if (parentGoal != null && parentGoal.Hash == Hash)
			{
				int i = 0;
				return null;
			}
			return parentGoal;

		}

		protected override void SetVariableOnEvent(Variable goalVariable)
		{
			if (IsEvent && ParentGoal != null)
			{
				ParentGoal.AddVariable(goalVariable);
			}
		}

		internal void Deconstruct(out object goal, out object error)
		{
			throw new NotImplementedException();
		}

		public Dictionary<string, string>? IncomingVariablesRequired { get; set; }
		[LlmIgnore]
		public string? DataSourceName { get; set; }
		[LlmIgnore]
		public bool IsSetup { get; set; }
		[LlmIgnore]
		public bool IsEvent { get; set; }

		[LlmIgnore]
		[IgnoreWhenInstructed]
		public Stopwatch Stopwatch { get; set; }

		public static Goal NotFound { get
			{
				return new Goal("NotFound", new());
			}
		}

		public static Goal Builder
		{
			get
			{
				return new Goal("Builder", new());
			}
		}
	}

	

}
