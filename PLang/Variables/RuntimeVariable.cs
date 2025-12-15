namespace PLang.Variables;

using System.Collections.Generic;

public class RuntimeVariable
{
	public string FullExpression { get; set; }
	public string VariableName { get; set; }
	public List<string> PropertyPaths { get; set; } = new List<string>();
	public int Start { get; set; }
	public int End { get; set; }
	public List<RuntimeOperation> Operations { get; set; } = new List<RuntimeOperation>();
}