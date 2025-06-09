using HtmlAgilityPack;
using PLang.Models;
using PLang.Models.ObjectValueConverters;
using PLang.Models.ObjectValueExtractors;
using PLang.Utils;

namespace PLang.Runtime;

public class DynamicObjectValue : ObjectValue
{
	Func<object> func;
	public DynamicObjectValue(string name, Func<object>? func, Type? type = null, ObjectValue? parent = null, bool Initiated = true, Properties? properties = null) : base(name, null, type, parent, Initiated, properties)
	{
		this.func = func;
	}

	public new object Value
	{
		get { return func(); }
	}
}

public class HtmlObjectValue : ObjectValue
{
	public HtmlObjectValue(string name, object? value, Type? type = null, ObjectValue? parent = null, bool Initiated = true, Properties? properties = null, bool isProperty = false) : base(name, value, type, parent, Initiated, properties, isProperty)
	{
	}

	public override object? ValueAs(ObjectValue objectValue, Type convertToType)
	{
		if (Value is HtmlNode node)
		{
			return node.InnerHtml;
		} else if (Value is List<HtmlNode> nodes)
		{
			if (nodes.Count == 1 && convertToType == typeof(string))
			{
				return nodes[0].InnerHtml;
			}

			List<string> strings = new();
			strings.AddRange(nodes.Select(p => p.OuterHtml));
			return strings;
		} else if (Value is HtmlNodeCollection col)
		{
			if (col.Count == 1 && convertToType == typeof(string))
			{
				return col[0].InnerHtml;
			}

			List<string> strings = new();
			strings.AddRange(col.Select(p => p.OuterHtml));
			return strings;

		}
			throw new NotImplementedException($"{convertToType} - {Value.GetType()}");

	}
}

public class ObjectValue
{
	private object? value;
	public static ObjectValue Null { get { return Nullable(""); } }
	public ObjectValue(string name, object? value, Type? type = null, ObjectValue? parent = null, bool Initiated = true, Properties? properties = null, bool isProperty = false)
	{
		name = VariableHelper.Clean(name);

		Name = name.ToLower();
		if (isProperty)
		{
			if (parent == null) throw new Exception("parent cannot be empty on property");
			this.Path = $"{parent.Name}!{name}";
		} else
		{
			this.Path = (parent != null) ? $"{parent.Name}.{name}" : name;
		}
			

		this.value = value;
		Type = type ?? value?.GetType();
		this.Initiated = Initiated;
		Parent = parent;
		Created = DateTime.Now;
		Updated = DateTime.Now;
		Properties = properties ?? new();
		IsProperty = isProperty;
	}

	public List<VariableEvent> Events = new List<VariableEvent>();
	public string Name { get; set; }

	
	public object? Value
	{
		get { return this.value; }
		set
		{
			this.value = value;
			Updated = DateTime.Now;

			if (Properties == null || Properties.Count == 0) return;

			var disposableProperties = Properties.Where(p => p is IDisposable).ToList();
			if (disposableProperties.Count > 0)
			{
				Task.Run(() =>
				{
					foreach (var item in Properties)
					{
						if (item.Value is IDisposable disposable)
						{
							disposable.Dispose();
						}
					}
				});
			}

		}
	}
	public Type? Type { get; set; }
	public bool Initiated { get; set; }
	public ObjectValue? Parent { get; set; }
	public Properties Properties { get; set; }
	public bool IsProperty { get; }
	public DateTime Created { get; private set; }
	public DateTime Updated { get; set; }
	public string Path { get; set; }
	public string PathAsVariable { get { return $"%{Path}%"; } }

	public T? Get<T>(string path, MemoryStack? memoryStack = null)
	{
		var value = Get(path, typeof(T), memoryStack);
		if (value == null) return default;
		return (T?)value;
	}

	public object? Get(string path, Type? convertToType = null, MemoryStack? memoryStack = null)
	{
		//%user.name% => %user.name!upper%
		//%user.address.zip% => %user.address.zip!int% (to force int)
		//%html% => %html!raw% (unprocessed, dangerous)
		//%dbResult% => %dbResult!sql%, %dbResult!parameters%, %dbResult!properties%
		//%names[0]% => %names[idx]% - memoryStack neeed
		var segments = PathSegmentParser.ParsePath(path);

		var objectValue = ObjectValueExtractor.Extract(this, segments, memoryStack);
		if (objectValue != null) objectValue.Path = this.Name + "." + path;

		if (convertToType == null || convertToType == typeof(ObjectValue)) return objectValue;
		return objectValue.ValueAs(objectValue, convertToType);
		
	}

	public override string? ToString()
	{
		return Value?.ToString();
	}

	public T? ValueAs<T>()
	{
		if (Value == null) return default;
		return (T?) ValueAs(this, typeof(T));
	}

	public virtual object? ValueAs(ObjectValue objectValue, Type convertToType)
	{
		return ObjectValueConverter.Convert(objectValue, convertToType);
		
	}

	public object? Math(string math)
	{
		// using NCalc, use could say % user.age * 2 / 4 ^ 16 %, it would translate to user.Math("* 2 / 4 ^ 16")
		return null;
	}

	public bool IsEmpty
	{
		get
		{
			if (!Initiated) return true;
			var isEmpty = VariableHelper.IsEmpty(Value);
			return isEmpty;
		}
	}

	public bool IsName(string variableName)
	{
		return Name.Equals(variableName, StringComparison.OrdinalIgnoreCase);
	}

	public bool Equals(object? obj, StringComparison? stringComparison)
	{
		if (Value == null && obj == null) return true;
		if (Value == null) return false;
		if (Value is string str)
		{
			var str2 = obj as string;
			return string.Equals(str, str2, stringComparison ?? StringComparison.OrdinalIgnoreCase);
		}
		return Value.Equals(obj);
	}

	public static ObjectValue Nullable(string? variableName, bool initiated = false)
	{
		return new ObjectValue(variableName ?? String.Empty, null, typeof(Nullable), null, initiated);
	}
}
