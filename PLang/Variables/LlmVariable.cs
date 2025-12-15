namespace PLang.Variables;

using System.Collections.Generic;
using System.ComponentModel;

public class LlmVariable
{
	[Description("The full variable expression as written in the code, e.g., '%name%' or '%user.name | to upper%'")]
	public string FullExpression { get; set; }

	[Description("The root variable name without operations, e.g., 'name', 'user', 'price'")]
	public string VariableName { get; set; }

	[Description("List of property paths where this variable is located. Examples: ['Text', 'Function.Parameters.textMessage.Content']")]
	public List<string> PropertyPaths { get; set; } = new List<string>();

	public List<Operation> Operations { get; set; } = new List<Operation>();
}