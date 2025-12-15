namespace PLang.Variables;

using System.Collections.Generic;

public class VariableMapping
{
	public string OriginalText { get; set; }
	public List<LlmVariable> Variables { get; set; } = new List<LlmVariable>();
}