﻿using Microsoft.AspNetCore.Mvc.TagHelpers;
using PLang.Attributes;
using PLang.Runtime;
using System.Reflection.Metadata;
using System.Runtime.Serialization;
using System.Text.Json.Serialization;

namespace PLang.Building.Model
{
	public record Variable(string VariableName, object Value)
	{
		[Newtonsoft.Json.JsonIgnore]
		[IgnoreDataMemberAttribute]
		[System.Text.Json.Serialization.JsonIgnore]
		public Func<Task>? DisposeFunc { get; init; }

		[Newtonsoft.Json.JsonIgnore]
		[IgnoreDataMemberAttribute]
		[System.Text.Json.Serialization.JsonIgnore]
		public GoalStep? Step { get; set; }
	};

	public abstract class VariableContainer
	{
		[Newtonsoft.Json.JsonIgnore]
		[IgnoreDataMemberAttribute]
		[System.Text.Json.Serialization.JsonIgnore]
		protected readonly List<Variable> _variables = new();

		[IgnoreWhenInstructed]
		public IReadOnlyList<Variable> Variables => _variables;

	
		public void AddVariable<T>(T? value, Func<Task>? func = null, string? variableName = null)
		{
			if (value == null) return;

			if (variableName == null) variableName = typeof(T).FullName;

			var variableIdx = _variables.FindIndex(p => p.VariableName.Equals(variableName, StringComparison.OrdinalIgnoreCase));
			var variable = new Variable(variableName, value) { DisposeFunc = func, Step = GetStep() };

			if (variableIdx == -1)
			{
				_variables.Add(variable);
			}
			else
			{
				_variables[variableIdx] = variable;
			}
			SetVariableOnEvent(variable);

		}
		public void AddVariable(Variable goalVariable)
		{
			var variableIdx = _variables.FindIndex(p => p.VariableName.Equals(goalVariable.VariableName, StringComparison.OrdinalIgnoreCase));
			if (variableIdx == -1)
			{
				_variables.Add(goalVariable);
			}
			else
			{
				_variables[variableIdx] = goalVariable;
			}
			SetVariableOnEvent(goalVariable);

		}
		public void AddVariables(List<Variable> variables)
		{
			foreach (var variable in variables)
			{
				AddVariable(variable);
			}
		}
		public List<Variable> GetVariables()
		{
			return _variables;
		}

		public List<T>? GetVariables<T>()
		{
			return _variables.Where(p => p.GetType() == typeof(T)).Select(p => (T)p.Value).ToList();
		}

		public object? GetVariable(string variableName, int level = 0)
		{
			var variable = _variables.FirstOrDefault(p => p.VariableName.Equals(variableName, StringComparison.OrdinalIgnoreCase));
			if (variable != null) return variable?.Value;

			if (level > 100)
			{
				var parent2 = GetParent();
				string goalName = (parent2 != null) ? parent2.GoalName : "";
				Console.ForegroundColor = ConsoleColor.Red;
				Console.WriteLine($"To deep GetVariable. variableName:{variableName} | parent2.goalName:{goalName}");
				Console.ResetColor();
				return null;
			}

			var parent = GetParent();
			if (parent == null) return default;

			var value = parent.GetVariable(variableName, ++level);
			return value;
		}

		protected abstract Goal? GetParent();
		protected abstract GoalStep? GetStep();
		protected abstract void SetVariableOnEvent(Variable goalVariable);

		public T? GetVariable<T>(string? variableName = null, int level = 0)
		{
			
			if (variableName == null) variableName = typeof(T).FullName;
			
			var variable = _variables.FirstOrDefault(p => p.VariableName.Equals(variableName, StringComparison.OrdinalIgnoreCase));
			if (variable != null) return (T?)variable?.Value;

			if (level > 100)
			{
				var parent2 = GetParent();
				string goalName = (parent2 != null) ? parent2.GoalName : "";
				Console.ForegroundColor = ConsoleColor.Red;
				Console.WriteLine($"To deep GetVariable. variableName:{variableName} | parent2.goalName:{goalName}");
				Console.ResetColor();
				return default;
			}

			var parent = GetParent();
			if (parent == null) return default;

			T? value = parent.GetVariable<T>(variableName, ++level);
			return value;
		}


		public bool RemoveVariable<T>(string? variableName = null)
		{
			if (variableName == null) variableName = typeof(T).FullName;
			return RemoveVariable(variableName);
		}
		public bool RemoveVariable(string? variableName = null)
		{
			var goalVariable = _variables.FirstOrDefault(p => p.VariableName.Equals(variableName, StringComparison.OrdinalIgnoreCase));
			if (goalVariable == null) return false;

			if (goalVariable.DisposeFunc != null) goalVariable.DisposeFunc();
			return _variables.Remove(goalVariable);

		}

		public void RemoveVariables<T>()
		{
			var variables = _variables.Where(p => p.GetType() == typeof(T));
			foreach (var variable in variables)
			{
				RemoveVariable(variable.VariableName);
			}

		}

		public async Task DisposeVariables(MemoryStack memoryStack)
		{
			for (int i = _variables.Count - 1; i >= 0; i--)
			{
				var variable = _variables[i];
				if (variable == null) continue;

				var parent = GetParent();
				if (parent != null && memoryStack.ContainsObject(variable))
				{
					parent.AddVariable(variable);
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
			_variables.Clear();
		}

	}
}
