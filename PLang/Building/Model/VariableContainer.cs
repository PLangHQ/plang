using PLang.Attributes;
using PLang.Errors;
using PLang.Runtime;
using System.Collections.Concurrent;
using System.Runtime.Serialization;

namespace PLang.Building.Model;

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
	protected ConcurrentDictionary<string, Variable> _variables = new(StringComparer.OrdinalIgnoreCase);

	[IgnoreWhenInstructed]
	public List<Variable> Variables
	{
		get => _variables.Values.ToList();
		set
		{
			_variables.Clear();
			if (value == null) return;

			foreach (var v in value)
			{
				_variables[v.VariableName] = v;
			}
		}
	}

	public void AddVariable<T>(T? value, Func<Task>? func = null, string? variableName = null)
	{
		if (value == null) return;

		variableName ??= typeof(T).FullName;
		if (variableName == null) return;

		var variable = new Variable(variableName, value) { DisposeFunc = func, Step = GetStep() };
		AddVariable(variable);
	}

	public void AddVariable(Variable? variable)
	{
		if (variable == null) return;

		_variables[variable.VariableName] = variable;
		SetVariableOnEvent(variable);
	}

	public void AddVariables(List<Variable>? variables)
	{
		if (variables == null) return;

		foreach (var variable in variables)
		{
			AddVariable(variable);
		}
	}

	public List<Variable> GetVariables()
	{
		return _variables.Values.ToList();
	}

	public List<T> GetVariables<T>()
	{
		return _variables.Values
			.Where(p => p.Value is T)
			.Select(p => (T)p.Value)
			.ToList();
	}

	public object? GetVariable(string? variableName, int level = 0)
	{
		if (string.IsNullOrEmpty(variableName)) return null;

		if (_variables.TryGetValue(variableName, out var variable))
		{
			return variable.Value;
		}

		if (level > 1000)
		{
			Console.ForegroundColor = ConsoleColor.Red;
			Console.WriteLine($"Too deep GetVariable. variableName:{variableName}");
			Console.ResetColor();
			return null;
		}

		var parent = GetParent();
		return parent?.GetVariable(variableName, level + 1);
	}

	public T? GetVariable<T>(string? variableName = null, int level = 0)
	{
		variableName ??= typeof(T).FullName;

		var value = GetVariable(variableName, level);
		if (value == null) return default;

		if (value is T typed)
		{
			return typed;
		}

		try
		{
			return (T)Convert.ChangeType(value, typeof(T));
		}
		catch
		{
			return default;
		}
	}

	public bool RemoveVariable<T>(string? variableName = null)
	{
		variableName ??= typeof(T).FullName;
		return RemoveVariable(variableName);
	}

	public bool RemoveVariable(string? variableName)
	{
		if (string.IsNullOrEmpty(variableName)) return false;

		if (_variables.TryRemove(variableName, out var removed))
		{
			removed.DisposeFunc?.Invoke();
			return true;
		}
		return false;
	}

	public async Task<bool> RemoveVariableAsync(string? variableName)
	{
		if (string.IsNullOrEmpty(variableName)) return false;

		if (_variables.TryRemove(variableName, out var removed))
		{
			if (removed.DisposeFunc != null)
			{
				await removed.DisposeFunc();
			}
			return true;
		}
		return false;
	}

	public void RemoveVariables<T>()
	{
		var toRemove = _variables.Values
			.Where(p => p.Value is T)
			.Select(p => p.VariableName)
			.ToList();

		foreach (var name in toRemove)
		{
			RemoveVariable(name);
		}
	}

	public async Task<IError?> DisposeVariables(MemoryStack memoryStack)
	{
		var parent = GetParent();
		IError? error = null;
		// Atomic swap - take ownership of old dictionary
		var oldVariables = _variables;
		_variables = new ConcurrentDictionary<string, Variable>(StringComparer.OrdinalIgnoreCase);

		var toDispose = oldVariables.Values
			.Where(v => !v.VariableName.StartsWith("!"))
			.ToList();

		foreach (var variable in toDispose)
		{
			if (parent != null && parent.Variables.Contains(variable))
			{
				continue;
			}

			if (memoryStack.ContainsObject(variable))
			{
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
					if (error == null)
					{
						error = new ExceptionError(ex, ex.Message, memoryStack.Goal);
					} else
					{
						error.ErrorChain.Add(new ExceptionError(ex, ex.Message, memoryStack.Goal));
					}
				}
			}
		}

		return error;
	}

	protected abstract CallStackFrame? GetParent();
	protected abstract GoalStep? GetStep();
	protected abstract void SetVariableOnEvent(Variable goalVariable);
}