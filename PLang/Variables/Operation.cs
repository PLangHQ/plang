namespace PLang.Variables;

using System.ComponentModel;

public class Operation
{
	[Description("The type or class the method belongs to, e.g., 'object', 'string', 'int'")]
	public string Class { get; set; }

	public string Method { get; set; }
	public object[] Parameters { get; set; } = System.Array.Empty<object>();
	public string ReturnType { get; set; }
}