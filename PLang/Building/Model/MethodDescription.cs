using PLang.Modules;
using System.Runtime.Serialization;

namespace PLang.Building.Model;

public class ClassDescription
{
	public ClassDescription()
	{
		Methods = new();
		SupportingObjects = new();
	}

	public string Information { get; set; }

	public List<MethodDescription> Methods { get; set; }
	public List<ComplexDescription> SupportingObjects { get; set; }
}
public class MethodDescription
{
	public string? Description { get; set; }
	public string MethodName { get; set; }
	public List<IPropertyDescription>? Parameters { get; set; }
	public ReturnValue ReturnValue { get; set; }
	public List<string>? Examples { get; set; } = null;
}

public interface IPropertyDescription
{
	public string? Description { get; set; }
	public string Type { get; set; }
	public string Name { get; set; }
	public object? DefaultValue { get; set; }
	public bool IsRequired { get; set; }

}
public class PrimitiveDescription : IPropertyDescription
{
	public string Type { get; set; }
	public string Name { get; set; }
	public object? DefaultValue { get; set; }
	public string? Description { get; set; }
	public bool IsRequired { get; set; }
}

public class ComplexDescription : IPropertyDescription
{
	public string Type { get; set; }
	public string Name { get; set; }
	public string? Description { get; set; }
	public object? DefaultValue { get; set; }
	public List<IPropertyDescription>? TypeProperties { get; set; }
	public bool IsRequired { get; set; }

	[Newtonsoft.Json.JsonIgnore]
	[IgnoreDataMemberAttribute]
	[System.Text.Json.Serialization.JsonIgnore]
	public List<string> MethodNames { get; set; } = new();
}

public class EnumDescription : IPropertyDescription
{
	public string Type { get; set; }
	public object? DefaultValue { get; set; }
	public string Name { get; set; }
	public string? Description { get; set; }
	public string? AvailableValues { get; set; }
	public bool IsRequired { get; set; }
}

public class ReturnValue
{
	public string Type { get; set; }
}