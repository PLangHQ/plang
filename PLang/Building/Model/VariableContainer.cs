using PLang.Attributes;
using PLang.Runtime;
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
			if (variableIdx == -1)
			{
				_variables.Add(new Variable(variableName, value) { DisposeFunc = func });
			}
			else
			{
				_variables[variableIdx] = new Variable(variableName, value) { DisposeFunc = func };
			}
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

		}

		public List<Variable> GetVariables()
		{
			return _variables;
		}

		public List<T>? GetVariables<T>()
		{
			return _variables.Where(p => p.GetType() == typeof(T)).Select(p => (T)p.Value).ToList();
		}

		public object? GetVariable(string variableName)
		{
			var variable = _variables.FirstOrDefault(p => p.VariableName.Equals(variableName, StringComparison.OrdinalIgnoreCase));
			if (variable != null) return variable?.Value;

			var parent = GetParent();
			if (parent == null) return default;

			var value = parent.GetVariable(variableName);
			return value;
		}

		protected abstract Goal? GetParent();

		public T? GetVariable<T>(string? variableName = null)
		{
			if (variableName == null) variableName = typeof(T).FullName;

			var variable = _variables.FirstOrDefault(p => p.VariableName.Equals(variableName, StringComparison.OrdinalIgnoreCase));
			if (variable != null) return (T?)variable?.Value;

			var parent = GetParent();
			if (parent == null) return default;

			T? value = parent.GetVariable<T>(variableName);
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
