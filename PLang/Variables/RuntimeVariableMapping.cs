namespace PLang.Variables;

using System.Collections.Generic;

public class RuntimeVariableMapping
{
	public string OriginalText { get; set; }
	public List<RuntimeVariable> Variables { get; set; } = new List<RuntimeVariable>();
}