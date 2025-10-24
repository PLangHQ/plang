using Microsoft.AspNetCore.Mvc.TagHelpers;
using PLang.Attributes;
using PLang.Runtime;
using System.Reflection.Metadata;
using System.Runtime.Serialization;
using System.Text.Json.Serialization;
using static PLang.Utils.VariableHelper;

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
		protected List<Variable> _variables = new();

		[IgnoreWhenInstructed]
		public List<Variable> Variables { get { return _variables; } set { _variables = value; } }

		
		public void AddVariable<T>(T? value, Func<Task>? func = null, string? variableName = null)
		{
			if (value == null) return;

			if (variableName == null) variableName = typeof(T).FullName;

			
			var variable = new Variable(variableName, value) { DisposeFunc = func, Step = GetStep() };

			var variableIdx = _variables.FindIndex(p => p.VariableName.Equals(variableName, StringComparison.OrdinalIgnoreCase));
			if (variableIdx == -1)
			{
				_variables.Add(variable);
			}
			else
			{
				try
				{
					_variables[variableIdx] = variable;
				} catch (Exception ex)
				{
					Console.WriteLine($"Exception - AddVariable:{ex.Message} - variableIdx:{variableIdx}");
					_variables.Add(variable);
				}
			}
			SetVariableOnEvent(variable);

		}
		public void AddVariable(Variable goalVariable)
		{
			if (goalVariable == null) return;

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

		public object? GetVariable(string variableName, int level = 0, List<string>? callStackGoals = null)
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
				return default;
			}

			var parent = GetParent();
			if (parent == null) return default;

			/*
			 * TODO: fix this
			 * 
			 * List<string> callStack is a fix, the ParentGoal should not be set on goal object
			 * it should be set on CallStack object that needs to be created, goal object should
			 * not change at runtime. this is because if same goal is called 2 or more times
			 * in a callstack, the parent goal is overwritten
			 * */
			if (callStackGoals?.Contains(parent.RelativePrPath) == true) return default;
			if (callStackGoals == null) callStackGoals = new();
			callStackGoals.Add(parent.RelativePrPath);


			var value = parent.GetVariable(variableName, ++level, callStackGoals);
			return value;
		}

		protected abstract Goal? GetParent();
		protected abstract GoalStep? GetStep();
		protected abstract void SetVariableOnEvent(Variable goalVariable);

		public T? GetVariable<T>(string? variableName = null, int level = 0, List<string>? callStackGoals = null)
		{
			
			if (variableName == null) variableName = typeof(T).FullName;
			
			var variable = _variables.FirstOrDefault(p => p.VariableName?.Equals(variableName, StringComparison.OrdinalIgnoreCase) == true);
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

			/*
			 * TODO: fix this
			 * 
			 * List<string> callStack is a fix, the ParentGoal should not be set on goal object
			 * it should be set on CallStack object that needs to be created, goal object should
			 * not change at runtime. this is because if same goal is called 2 or more times
			 * in a callstack, the parent goal is overwritten
			 * */
			if (callStackGoals?.Contains(parent.RelativePrPath) == true) return default;
			if (callStackGoals == null) callStackGoals = new();
			callStackGoals.Add(parent.RelativePrPath);


			T? value = parent.GetVariable<T>(variableName, ++level, callStackGoals);
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
					//parent.AddVariable(variable);
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
			_variables = new();

		}

	}
}
