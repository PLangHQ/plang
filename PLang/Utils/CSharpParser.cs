using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using System.Reflection;

public class CSharpParser
{
	private SyntaxTree _syntaxTree;
	private CompilationUnitSyntax _root;
	private string _sourceCode;

	private readonly string[] _ignoreAttributes = { "LlmIgnore", "JsonIgnore" };

	public void LoadCode(string sourceCode)
	{
		_sourceCode = sourceCode;
		_syntaxTree = CSharpSyntaxTree.ParseText(sourceCode);
		_root = _syntaxTree.GetCompilationUnitRoot();
	}

	public List<MethodInformation> GetMethods()
	{
		var methods = new List<MethodInformation>();

		var methodDeclarations = _root.DescendantNodes().OfType<MethodDeclarationSyntax>();

		foreach (var method in methodDeclarations)
		{
			methods.Add(new MethodInformation
			{
				Name = method.Identifier.Text,
				ReturnType = method.ReturnType.ToString(),
				Syntax = method,
				Source = GetSourceFromSpan(method.Span)
			});
		}

		return methods;
	}

	public List<ParameterInformation> GetParameters(MethodInformation method)
	{
		var parameters = new List<ParameterInformation>();

		foreach (var param in method.Syntax.ParameterList.Parameters)
		{
			if (HasIgnoreAttribute(param.AttributeLists))
				continue;

			parameters.Add(new ParameterInformation
			{
				Name = param.Identifier.Text,
				TypeName = param.Type?.ToString() ?? "unknown",
				Syntax = param,
				Source = GetSourceFromSpan(param.Span)
			});
		}

		return parameters;
	}

	public List<RecordInformation> GetRecords()
	{
		var records = new List<RecordInformation>();

		var recordDeclarations = _root.DescendantNodes().OfType<RecordDeclarationSyntax>();

		foreach (var record in recordDeclarations)
		{
			var properties = new List<PropertyInformation>();
			string constructorSource = null;

			if (record.ParameterList != null)
			{
				constructorSource = record.Identifier.Text + GetSourceFromSpan(record.ParameterList.Span);

				foreach (var param in record.ParameterList.Parameters)
				{
					if (HasIgnoreAttribute(param.AttributeLists))
						continue;

					properties.Add(new PropertyInformation
					{
						Name = param.Identifier.Text,
						TypeName = param.Type?.ToString() ?? "unknown",
						IsFromPrimaryConstructor = true,
						Source = GetSourceFromSpan(param.Span)
					});
				}
			}

			var propertyDeclarations = record.Members.OfType<PropertyDeclarationSyntax>();
			foreach (var prop in propertyDeclarations)
			{
				if (HasIgnoreAttribute(prop.AttributeLists))
					continue;

				properties.Add(new PropertyInformation
				{
					Name = prop.Identifier.Text,
					TypeName = prop.Type.ToString(),
					IsFromPrimaryConstructor = false,
					Source = GetSourceFromSpan(prop.Span)
				});
			}

			var explicitConstructor = record.Members.OfType<ConstructorDeclarationSyntax>().FirstOrDefault();
			if (explicitConstructor != null)
			{
				constructorSource = GetSourceFromSpan(explicitConstructor.Span);
			}

			records.Add(new RecordInformation
			{
				Name = record.Identifier.Text,
				Properties = properties,
				Syntax = record,
				Source = GetSourceFromSpan(record.Span),
				ConstructorSource = constructorSource
			});
		}

		return records;
	}

	public List<ClassInformation> GetClasses()
	{
		var classes = new List<ClassInformation>();

		var classDeclarations = _root.DescendantNodes().OfType<ClassDeclarationSyntax>();

		foreach (var cls in classDeclarations)
		{
			var properties = new List<PropertyInformation>();
			var fields = new List<FieldInformation>();
			string constructorSource = null;

			var propertyDeclarations = cls.Members.OfType<PropertyDeclarationSyntax>();
			foreach (var prop in propertyDeclarations)
			{
				if (HasIgnoreAttribute(prop.AttributeLists))
					continue;

				properties.Add(new PropertyInformation
				{
					Name = prop.Identifier.Text,
					TypeName = prop.Type.ToString(),
					IsFromPrimaryConstructor = false,
					Source = GetSourceFromSpan(prop.Span)
				});
			}

			var fieldDeclarations = cls.Members.OfType<FieldDeclarationSyntax>();
			foreach (var field in fieldDeclarations)
			{
				if (HasIgnoreAttribute(field.AttributeLists))
					continue;

				foreach (var variable in field.Declaration.Variables)
				{
					fields.Add(new FieldInformation
					{
						Name = variable.Identifier.Text,
						TypeName = field.Declaration.Type.ToString(),
						Source = GetSourceFromSpan(field.Span)
					});
				}
			}

			var constructor = cls.Members.OfType<ConstructorDeclarationSyntax>().FirstOrDefault();
			if (constructor != null)
			{
				constructorSource = GetSourceFromSpan(constructor.Span);
			}

			classes.Add(new ClassInformation
			{
				Name = cls.Identifier.Text,
				Properties = properties,
				Fields = fields,
				Syntax = cls,
				Source = GetSourceFromSpan(cls.Span),
				ConstructorSource = constructorSource
			});
		}

		return classes;
	}

	public List<EnumInformation> GetEnums()
	{
		var enums = new List<EnumInformation>();

		var enumDeclarations = _root.DescendantNodes().OfType<EnumDeclarationSyntax>();

		foreach (var enumDecl in enumDeclarations)
		{
			var members = new List<EnumMemberInformation>();

			foreach (var member in enumDecl.Members)
			{
				members.Add(new EnumMemberInformation
				{
					Name = member.Identifier.Text,
					Value = member.EqualsValue?.Value.ToString(),
					Source = GetSourceFromSpan(member.Span)
				});
			}

			enums.Add(new EnumInformation
			{
				Name = enumDecl.Identifier.Text,
				Members = members,
				Syntax = enumDecl,
				Source = GetSourceFromSpan(enumDecl.Span)
			});
		}

		return enums;
	}

	public TypeInformation? GetType(string typeName)
	{
		typeName = StripTypeName(typeName);

		var classDecl = _root.DescendantNodes()
			.OfType<ClassDeclarationSyntax>()
			.FirstOrDefault(c => c.Identifier.Text == typeName);

		if (classDecl != null)
		{
			string constructorSource = null;
			var constructor = classDecl.Members.OfType<ConstructorDeclarationSyntax>().FirstOrDefault();
			if (constructor != null)
			{
				constructorSource = GetSourceFromSpan(constructor.Span);
			}

			return new TypeInformation
			{
				Name = classDecl.Identifier.Text,
				Kind = "class",
				Source = GetSourceFromSpan(classDecl.Span),
				ConstructorSource = constructorSource
			};
		}

		var recordDecl = _root.DescendantNodes()
			.OfType<RecordDeclarationSyntax>()
			.FirstOrDefault(r => r.Identifier.Text == typeName);

		if (recordDecl != null)
		{
			string constructorSource = null;

			if (recordDecl.ParameterList != null)
			{
				constructorSource = recordDecl.Identifier.Text + GetSourceFromSpan(recordDecl.ParameterList.Span);
			}

			var explicitConstructor = recordDecl.Members.OfType<ConstructorDeclarationSyntax>().FirstOrDefault();
			if (explicitConstructor != null)
			{
				constructorSource = GetSourceFromSpan(explicitConstructor.Span);
			}

			return new TypeInformation
			{
				Name = recordDecl.Identifier.Text,
				Kind = "record",
				Source = GetSourceFromSpan(recordDecl.Span),
				ConstructorSource = constructorSource
			};
		}

		var structDecl = _root.DescendantNodes()
			.OfType<StructDeclarationSyntax>()
			.FirstOrDefault(s => s.Identifier.Text == typeName);

		if (structDecl != null)
		{
			string constructorSource = null;
			var constructor = structDecl.Members.OfType<ConstructorDeclarationSyntax>().FirstOrDefault();
			if (constructor != null)
			{
				constructorSource = GetSourceFromSpan(constructor.Span);
			}

			return new TypeInformation
			{
				Name = structDecl.Identifier.Text,
				Kind = "struct",
				Source = GetSourceFromSpan(structDecl.Span),
				ConstructorSource = constructorSource
			};
		}

		var interfaceDecl = _root.DescendantNodes()
			.OfType<InterfaceDeclarationSyntax>()
			.FirstOrDefault(i => i.Identifier.Text == typeName);

		if (interfaceDecl != null)
		{
			return new TypeInformation
			{
				Name = interfaceDecl.Identifier.Text,
				Kind = "interface",
				Source = GetSourceFromSpan(interfaceDecl.Span),
				ConstructorSource = null
			};
		}

		var enumDecl = _root.DescendantNodes()
			.OfType<EnumDeclarationSyntax>()
			.FirstOrDefault(e => e.Identifier.Text == typeName);

		if (enumDecl != null)
		{
			var members = new List<EnumMemberInformation>();
			foreach (var member in enumDecl.Members)
			{
				members.Add(new EnumMemberInformation
				{
					Name = member.Identifier.Text,
					Value = member.EqualsValue?.Value.ToString(),
					Source = GetSourceFromSpan(member.Span)
				});
			}

			return new TypeInformation
			{
				Name = enumDecl.Identifier.Text,
				Kind = "enum",
				Source = GetSourceFromSpan(enumDecl.Span),
				ConstructorSource = null,
				EnumMembers = members
			};
		}

		return null;
	}

	public List<TypeInformation> GetConstructorTypes(TypeInformation typeInfo)
	{
		var types = new List<TypeInformation>();
		var parameterTypeNames = new List<string>();

		if (typeInfo.Kind == "record")
		{
			var recordDecl = _root.DescendantNodes()
				.OfType<RecordDeclarationSyntax>()
				.FirstOrDefault(r => r.Identifier.Text == typeInfo.Name);

			if (recordDecl != null)
			{
				if (recordDecl.ParameterList != null)
				{
					foreach (var param in recordDecl.ParameterList.Parameters)
					{
						if (HasIgnoreAttribute(param.AttributeLists))
							continue;

						if (param.Type != null)
							parameterTypeNames.AddRange(ExtractTypeNames(param.Type.ToString()));
					}
				}

				var explicitConstructor = recordDecl.Members.OfType<ConstructorDeclarationSyntax>().FirstOrDefault();
				if (explicitConstructor != null)
				{
					foreach (var param in explicitConstructor.ParameterList.Parameters)
					{
						if (HasIgnoreAttribute(param.AttributeLists))
							continue;

						if (param.Type != null)
							parameterTypeNames.AddRange(ExtractTypeNames(param.Type.ToString()));
					}
				}
			}
		}
		else if (typeInfo.Kind == "class")
		{
			var classDecl = _root.DescendantNodes()
				.OfType<ClassDeclarationSyntax>()
				.FirstOrDefault(c => c.Identifier.Text == typeInfo.Name);

			if (classDecl != null)
			{
				var constructor = classDecl.Members.OfType<ConstructorDeclarationSyntax>().FirstOrDefault();
				if (constructor != null)
				{
					foreach (var param in constructor.ParameterList.Parameters)
					{
						if (HasIgnoreAttribute(param.AttributeLists))
							continue;

						if (param.Type != null)
							parameterTypeNames.AddRange(ExtractTypeNames(param.Type.ToString()));
					}
				}
			}
		}
		else if (typeInfo.Kind == "struct")
		{
			var structDecl = _root.DescendantNodes()
				.OfType<StructDeclarationSyntax>()
				.FirstOrDefault(s => s.Identifier.Text == typeInfo.Name);

			if (structDecl != null)
			{
				var constructor = structDecl.Members.OfType<ConstructorDeclarationSyntax>().FirstOrDefault();
				if (constructor != null)
				{
					foreach (var param in constructor.ParameterList.Parameters)
					{
						if (HasIgnoreAttribute(param.AttributeLists))
							continue;

						if (param.Type != null)
							parameterTypeNames.AddRange(ExtractTypeNames(param.Type.ToString()));
					}
				}
			}
		}

		var distinctTypeNames = parameterTypeNames.Distinct();
		foreach (var typeName in distinctTypeNames)
		{
			var resolvedType = GetType(typeName);
			if (resolvedType != null)
			{
				types.Add(resolvedType);
			}
		}

		return types;
	}

	private string GetSourceFromSpan(TextSpan span)
	{
		return _sourceCode.Substring(span.Start, span.Length).ReplaceLineEndings(" ").Replace("\t", " ").Replace("( ", "(").Replace(" )", ")").Replace("  ", " ");
	}

	private bool HasIgnoreAttribute(SyntaxList<AttributeListSyntax> attributeLists)
	{
		foreach (var attributeList in attributeLists)
		{
			foreach (var attribute in attributeList.Attributes)
			{
				var name = attribute.Name.ToString();
				foreach (var ignoreAttr in _ignoreAttributes)
				{
					if (name == ignoreAttr || name == $"{ignoreAttr}Attribute")
						return true;
				}
			}
		}
		return false;
	}

	private string StripTypeName(string typeName)
	{
		typeName = typeName.TrimEnd('?');

		var bracketIndex = typeName.IndexOf('[');
		if (bracketIndex > 0)
			typeName = typeName.Substring(0, bracketIndex);

		var genericIndex = typeName.IndexOf('<');
		if (genericIndex > 0)
			typeName = typeName.Substring(0, genericIndex);

		return typeName.Trim();
	}

	private List<string> ExtractTypeNames(string typeName)
	{
		var typeNames = new List<string>();

		typeName = typeName.TrimEnd('?');

		var bracketIndex = typeName.IndexOf('[');
		if (bracketIndex > 0)
			typeName = typeName.Substring(0, bracketIndex);

		var genericIndex = typeName.IndexOf('<');
		if (genericIndex > 0)
		{
			var outerType = typeName.Substring(0, genericIndex).Trim();
			typeNames.Add(outerType);

			var innerStart = genericIndex + 1;
			var innerEnd = typeName.LastIndexOf('>');
			if (innerEnd > innerStart)
			{
				var innerTypes = typeName.Substring(innerStart, innerEnd - innerStart);
				var innerTypeList = SplitGenericArguments(innerTypes);
				foreach (var inner in innerTypeList)
				{
					typeNames.AddRange(ExtractTypeNames(inner.Trim()));
				}
			}
		}
		else
		{
			typeNames.Add(typeName.Trim());
		}

		return typeNames;
	}

	private List<string> SplitGenericArguments(string arguments)
	{
		var result = new List<string>();
		var depth = 0;
		var current = "";

		foreach (var c in arguments)
		{
			if (c == '<')
			{
				depth++;
				current += c;
			}
			else if (c == '>')
			{
				depth--;
				current += c;
			}
			else if (c == ',' && depth == 0)
			{
				result.Add(current.Trim());
				current = "";
			}
			else
			{
				current += c;
			}
		}

		if (!string.IsNullOrWhiteSpace(current))
			result.Add(current.Trim());

		return result;
	}
}

public class MethodInformation
{
	public string Name { get; set; }
	public string ReturnType { get; set; }
	public string Source { get; set; }
	public MethodDeclarationSyntax Syntax { get; set; }
}

public class ParameterInformation
{
	public string Name { get; set; }
	public string TypeName { get; set; }
	public string Source { get; set; }
	public ParameterSyntax Syntax { get; set; }
}

public class TypeInformation
{
	public string Name { get; set; }
	public string Kind { get; set; }
	public string Source { get; set; }
	public string ConstructorSource { get; set; }
	public List<EnumMemberInformation> EnumMembers { get; set; }
}

public class PropertyInformation
{
	public string Name { get; set; }
	public string TypeName { get; set; }
	public bool IsFromPrimaryConstructor { get; set; }
	public string Source { get; set; }
}

public class FieldInformation
{
	public string Name { get; set; }
	public string TypeName { get; set; }
	public string Source { get; set; }
}

public class RecordInformation
{
	public string Name { get; set; }
	public List<PropertyInformation> Properties { get; set; }
	public string Source { get; set; }
	public string ConstructorSource { get; set; }
	public RecordDeclarationSyntax Syntax { get; set; }
}

public class ClassInformation
{
	public string Name { get; set; }
	public List<PropertyInformation> Properties { get; set; }
	public List<FieldInformation> Fields { get; set; }
	public string Source { get; set; }
	public string ConstructorSource { get; set; }
	public ClassDeclarationSyntax Syntax { get; set; }
}

public class EnumInformation
{
	public string Name { get; set; }
	public List<EnumMemberInformation> Members { get; set; }
	public string Source { get; set; }
	public EnumDeclarationSyntax Syntax { get; set; }
}

public class EnumMemberInformation
{
	public string Name { get; set; }
	public string Value { get; set; }
	public string Source { get; set; }
}