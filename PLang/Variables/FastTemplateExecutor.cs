namespace PLang.Variables;

using System.Collections.Generic;
using System.Text;

public class FastTemplateExecutor
{
	private readonly Dictionary<string, object> _variables;
	private readonly PipeExecutor _pipeExecutor;

	public FastTemplateExecutor(Dictionary<string, object> variables)
	{
		_variables = variables;
		_pipeExecutor = new PipeExecutor(variables);
	}

	public string Execute(RuntimeVariableMapping template)
	{
		if (template.Variables.Count == 0)
		{
			return template.OriginalText;
		}

		// Sort variables by start position to process in order
		var sortedVariables = template.Variables.OrderBy(v => v.Start).ToList();

		// Pre-allocate StringBuilder with estimated capacity
		var estimatedLength = template.OriginalText.Length;
		var builder = new StringBuilder(estimatedLength * 2);

		int lastPosition = 0;

		foreach (var variable in sortedVariables)
		{
			// Add text before this variable
			if (variable.Start > lastPosition)
			{
				builder.Append(template.OriginalText, lastPosition, variable.Start - lastPosition);
			}

			// Execute the variable and append result
			var value = ExecuteVariable(variable);
			builder.Append(value);

			lastPosition = variable.End;
		}

		// Add remaining text after last variable
		if (lastPosition < template.OriginalText.Length)
		{
			builder.Append(template.OriginalText, lastPosition, template.OriginalText.Length - lastPosition);
		}

		return builder.ToString();
	}

	private object ExecuteVariable(RuntimeVariable variable)
	{
		if (!_variables.ContainsKey(variable.VariableName))
		{
			throw new KeyNotFoundException($"Variable '{variable.VariableName}' not found");
		}

		// If no operations, just return the variable value
		if (variable.Operations.Count == 0)
		{
			return _variables[variable.VariableName];
		}

		// Execute the pipeline
		var pipeline = new PipelineResult { Operations = variable.Operations };
		return _pipeExecutor.Execute(variable.VariableName, pipeline);
	}
}