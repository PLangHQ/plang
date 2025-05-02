using PLang.Modules;

namespace PLang.Building.Model;

public class MethodDescription
{
	public string? Description { get; set; }
	public string MethodName { get; set; }
	public List<IPropertyDescription>? Parameters { get; set; }
	public ReturnValue ReturnValue { get; set; }
}

public interface IPropertyDescription
{
	public string Description { get; set; }
	public string Type { get; set; }
	public string Name { get; set; }
	public object? DefaultValue { get; set; }

}
public class PrimitiveDescription : IPropertyDescription
{
	public string Type { get; set; }
	public string Name { get; set; }
	public object? DefaultValue { get; set; }
	public string Description { get; set; }
}

public class ComplexDescription : IPropertyDescription
{
	public string Type { get; set; }
	public string Name { get; set; }
	public string Description { get; set; }
	public object? DefaultValue { get; set; }
	public List<IPropertyDescription>? TypeProperties { get; set; }
}

public class EnumDescription : IPropertyDescription
{
	public string Type { get; set; }
	public object? DefaultValue { get; set; }
	public string Name { get; set; }
	public string Description { get; set; }
	public string AvailableValues { get; set; }
}

public class ReturnValue
{
	public string Type { get; set; }
}