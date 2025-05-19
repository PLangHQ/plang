using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using PLang.Modules;
using PLang.Runtime;
using System;
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
		public GoalInfo GoalInfo { get; set; }

		public List<Injections> Injections { get; set; }

		//Signature should be used when goal is deployed
		//this allows for validating the publisher and that code has not changed.
		public string Signature { get; set; }
		
		[Newtonsoft.Json.JsonIgnore]
		[IgnoreDataMemberAttribute]
		[System.Text.Json.Serialization.JsonIgnore]
		public bool HasChanged { get; set; }
		public string FileHash { get; set; }
		public string Hash { get; set; }

		[Newtonsoft.Json.JsonIgnore]
		[IgnoreDataMemberAttribute]

		[System.Text.Json.Serialization.JsonIgnore]
		public Goal? ParentGoal { get; set; }

		[Newtonsoft.Json.JsonIgnore]
		[IgnoreDataMemberAttribute]

		[System.Text.Json.Serialization.JsonIgnore]
		public bool IsOS { get; set; }
		[Newtonsoft.Json.JsonIgnore]
		[IgnoreDataMemberAttribute]

		[System.Text.Json.Serialization.JsonIgnore]
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

		public Dictionary<string, string>? IncomingVariablesRequired { get; set; }
		public string? DataSourceName { get; set; }
		public bool IsSetup { get; set; }


		[Newtonsoft.Json.JsonIgnore]
		[IgnoreDataMemberAttribute]
		[System.Text.Json.Serialization.JsonIgnore]
		List<GoalVariable> Variables = new();
		public void AddVariable<T>(T? value, Func<Task>? func = null, string? variableName = null)
		{
			if (value == null) return;

			if (variableName == null) variableName = typeof(T).Name;
			
			var variableIdx = Variables.FindIndex(p => p.VariableName.Equals(variableName, StringComparison.OrdinalIgnoreCase));
			if (variableIdx == -1)
			{
				Variables.Add(new GoalVariable(variableName, value) {  DisposeFunc = func });
			} else
			{
				Variables[variableIdx] = new GoalVariable(variableName, value) {  DisposeFunc = func };
			}
		}
		public void AddVariable(GoalVariable goalVariable)
		{
			var variableIdx = Variables.FindIndex(p => p.VariableName.Equals(goalVariable.VariableName, StringComparison.OrdinalIgnoreCase));
			if (variableIdx == -1)
			{
				Variables.Add(goalVariable);
			}
			else
			{
				Variables[variableIdx] = goalVariable;
			}
			
		}

		public List<GoalVariable> GetVariables()
		{
			return Variables;
		}
		public T? GetVariable<T>(string? variableName = null)
		{
			if (variableName == null) variableName = typeof(T).Name;

			var variable = Variables.FirstOrDefault(p => p.VariableName == variableName);
			if (variable != null) return (T?)variable?.Value;

			if (ParentGoal == null) return default;

			T? value = ParentGoal.GetVariable<T>(variableName);
			return value;
		}
		public void RemoveVariable<T>(string? variableName = null)
		{
			if (variableName == null) variableName = typeof(T).Name;
			RemoveVariable(variableName);
		}
		public void RemoveVariable(string? variableName = null)
		{
			var goalVariable = Variables.FirstOrDefault(p => p.VariableName == variableName);
			if (goalVariable == null) return;

			if (goalVariable.DisposeFunc != null) goalVariable.DisposeFunc();
			Variables.Remove(goalVariable);

		}

		public async Task DisposeVariables(MemoryStack memoryStack)
		{
			for (int i = Variables.Count - 1; i >= 0; i--)
			{
				var variable = Variables[i];
				if (ParentGoal != null && memoryStack.ContainsObject(variable))
				{
					ParentGoal.AddVariable(variable);
					continue;
				}

				if (variable.DisposeFunc != null)
				{
					try
					{
						await variable.DisposeFunc();
					}
					catch (Exception ex)
					{
						Console.WriteLine("Exception on Dispose:" + ex);
					}
				}
			}
			Variables.Clear();
		}
	}

	public record GoalVariable(string VariableName, object Value)
	{
		[JsonIgnore]
		public Func<Task>? DisposeFunc { get; init; }
	};

}
