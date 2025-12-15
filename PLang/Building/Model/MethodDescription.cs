using Nethereum.Model;
using Newtonsoft.Json;
using PLang.Modules;
using PLang.Utils;
using System.Runtime.Serialization;
using System.Text;
using System.Xml.Linq;

namespace PLang.Building.Model;

public class ClassDescription
{
	public string Name { get; set; }
	public ClassDescription()
	{
		Methods = new();
		SupportingObjects = new();
	}

	public string Description { get; set; }
	public string ExampleInformation { get; set; }

	public List<MethodDescription> Methods { get; set; }
	public List<ComplexDescription> SupportingObjects { get; set; }
	public List<EnumDescription> SupportingEnums { get; set; } = new();
}

public record PlangExample(string code, string mapping);
public class MethodDescription
{
	public string? Description { get; set; }
	public string MethodName { get; set; }
	public List<IPropertyDescription>? Parameters { get; set; }
	public ReturnValue ReturnValue { get; set; }
	public List<PlangExample>? Examples { get; set; } = new();

	public string MethodSignature
	{
		get
		{
			StringBuilder sb = new();
			sb.Append($"{MethodName}(");
			for (int i=0;i<Parameters?.Count;i++)
			{
				if (i != 0) sb.Append(", ");
				sb.Append(Parameters[i].ParameterSignature);
			}
			if (ReturnValue == null)
			{
				sb.Append($") : void");
			}
			else
			{
				sb.Append($") : {ReturnValue.ReturnValueSignature}");
			}
			return sb.ToString();
		}
	}
}

public static class PropertySignatureHelper
{
	public static string Format(IPropertyDescription desc)
	{
		StringBuilder sb = new();
		if (!string.IsNullOrWhiteSpace(desc.Description))
		{
			if (desc.Description.Contains('\n'))
			{
				sb.AppendLine($"/*\n{desc.Description.Trim()}\n*/");
			}
			else
			{
				sb.AppendLine($"// {desc.Description.Trim()}");
			}
		}
		sb.Append(desc.Type);
		if (!desc.IsRequired) sb.Append("?");
		sb.Append($" {desc.Name}");
		if (desc.DefaultValue != null)
		{
			sb.Append($" = {desc.DefaultValue.ToString()}");
		}
		return sb.ToString();
	}
}

public interface IPropertyDescription
{
	public string? Description { get; set; }
	public string Type { get; set; }
	public string Name { get; set; }
	public object? DefaultValue { get; set; }
	public bool IsRequired { get; set; }

	public string ParameterSignature
	{
		get
		{
			return PropertySignatureHelper.Format(this);
		}
	}
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

	public string? ParameterSignature
	{
		get
		{
			StringBuilder sb = new StringBuilder();
			if (TypeProperties == null)
			{
				return PropertySignatureHelper.Format(this);
			}

			
			if (!string.IsNullOrEmpty(Description))
			{
				if (Description.Contains('\n'))
				{
					sb.AppendLine($"/*\n{Description.Trim()}\n*/");
				}
				else
				{
					sb.AppendLine($"// {Description.Trim()}");
				}
			}
			sb.Append($"{Type}(");
			for (int i = 0;i < TypeProperties.Count;i++) {
				if (i != 0) sb.Append(", ");
				sb.AppendLine(PropertySignatureHelper.Format(TypeProperties[i]));
			}

			sb.Append($")\nUsed in methods: {string.Join(", ", MethodNames)}");
			return sb.ToString();
		}
	}
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
	public string ReturnValueSignature
	{
		get
		{
			return Type;
		}
	}
}