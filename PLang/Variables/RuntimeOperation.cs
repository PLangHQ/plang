namespace PLang.Variables;

using System.Reflection;

public class RuntimeOperation
{
	public string Class { get; set; }
	public string Method { get; set; }
	public object[] Parameters { get; set; }
	public string ReturnType { get; set; }
	public MethodInfo MethodInfo { get; set; }
}