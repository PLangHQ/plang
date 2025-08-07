using HtmlAgilityPack;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using PLang.Errors;
using PLang.Models;
using PLang.Models.ObjectValueConverters;
using PLang.Models.ObjectValueExtractors;
using PLang.Utils;
using System.Diagnostics;
using System.Xml.Linq;
using Websocket.Client.Logging;

namespace PLang.Runtime;

public class DynamicObjectValue : ObjectValue
{
	Func<object> func;
	public DynamicObjectValue(string name, Func<object> func, Type? type = null, ObjectValue? parent = null, bool Initiated = true, Properties? properties = null, bool isProperty = false, bool isSystemVariable = false) : base(name, null, type, parent, Initiated, properties, isProperty, isSystemVariable)
	{
		this.func = func;
	}

	public override object Value
	{
		get { 
			var value = func();

			if (value is ObjectValue ov) return ov.Value;
			return value;
		}
	}

	public new Properties Properties
	{
		get
		{
			var value = func();
			if (value is ObjectValue ov) return ov.Properties;
			return base.Properties;
		}
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
		}
		else if (Value is List<HtmlNode> nodes)
		{
			if (nodes.Count == 1 && convertToType == typeof(string))
			{
				return nodes[0].InnerHtml;
			}

			List<string> strings = new();
			strings.AddRange(nodes.Select(p => p.OuterHtml));
			return strings;
		}
		else if (Value is HtmlNodeCollection col)
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
	private ObjectValue? parent;
	public static ObjectValue Null { get { return Nullable(""); } }
	public ObjectValue(string name, object? value, Type? type = null, ObjectValue? parent = null, bool Initiated = true, Properties? properties = null, bool isProperty = false, bool isSystemVariable = false)
	{
		name = VariableHelper.Clean(name);
		if (name.Contains("!") && !name.StartsWith("!"))
		{
			int i = 0;
		}
		Name = name;
		if (isProperty)
		{
			if (parent == null) throw new Exception("parent cannot be empty on property");
			this.Path = $"{parent.Path}!{name}";
		}
		else
		{
			//not happy with this check, a variable could be %product._id%, not sure if this is enough
			//are there some other versions of variable? this one came about because of elastic search
			string prefix = (Char.IsLetterOrDigit(name[0]) || name[0] == '_') ? "." : "";
			this.Path = (parent != null) ? $"{parent.Path}{prefix}{name}" : name;
		}

		if (value is string str)
		{
			this.value = str;// str.Trim();
		}
		else
		{
			this.value = value;
		}
		Type = type ?? value?.GetType();
		this.Initiated = Initiated;
		this.parent = parent;
		Created = DateTime.Now;
		Updated = DateTime.Now;
		Properties = properties ?? new();
		
		foreach (var prop in Properties)
		{
			prop.Parent = this;
		}

		IsProperty = isProperty;
		IsSystemVariable = isSystemVariable;
	}

	public List<VariableEvent> Events = new List<VariableEvent>();
	public string Name { get; }
	[JsonIgnore]
	public ObjectValue Root
	{
		get
		{
			var parent = Parent;
			if (parent == null) return this;

			while (parent.Parent != null)
			{
				parent = parent.Parent;
			}
			return parent;
		}
	}
	public bool IsSystemVariable { get; set; }
	public virtual object? Value
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
	[JsonIgnore]
	public ObjectValue? Parent { 
		get { return parent; }
		set {
			if (value == null) throw new NullReferenceException($"setting null parent in ObjectValue is not allowed. {ErrorReporting.CreateIssueShouldNotHappen}");

			parent = value;
			
			this.Path = $"{parent.Path}!{Name}";
		}
	}
	public Properties Properties { get; set; }
	public bool IsProperty { get; }
	public DateTime Created { get; private set; }
	public DateTime Updated { get; set; }
	public string Path { get; set; }
	public string PathAsVariable { get { return $"%{Path}%"; } }
	[JsonIgnore]
	public int Order { get; set; } = 999;
	public T? Get<T>(string path, MemoryStack? memoryStack = null)
	{
		var value = Get(path, typeof(T), memoryStack);
		if (value == null) return default;
			
		return (T?)value;
	}
	public ObjectValue GetObjectValue(string path, Type? convertToType = null, MemoryStack? memoryStack = null)
	{
		if (string.IsNullOrEmpty(path)) return ObjectValue.Null;

		var segments = PathSegmentParser.ParsePath(path, memoryStack);
		var objectValue = ObjectValueExtractor.Extract(this, segments, memoryStack);
		if (convertToType == null || convertToType == typeof(ObjectValue)) return objectValue;
		if (objectValue == null) return ObjectValue.Nullable(path, this.Initiated);

		return objectValue;
	}
	public object? Get(string path, Type? convertToType = null, MemoryStack? memoryStack = null)
	{

		//%user.name% => %user.name!upper%
		//%user.address.zip% => %user.address.zip!int% (to force int)
		//%html% => %html!raw% (unprocessed, dangerous)
		//%dbResult% => %dbResult!sql%, %dbResult!parameters%, %dbResult!properties%
		//%names[0]% => %names[idx]% - memoryStack needed
		var objectValue = GetObjectValue(path, convertToType, memoryStack);
		if (convertToType == typeof(ObjectValue) || convertToType == null) return objectValue;

		return objectValue.ValueAs(objectValue, convertToType);

	}

	public override string? ToString()
	{
		return Value?.ToString();
	}

	public T? ValueAs<T>()
	{
		if (Value == null) return default;
		return (T?)ValueAs(this, typeof(T));
	}

	public virtual object? ValueAs(ObjectValue objectValue, Type convertToType)
	{
		return Models.ObjectValueConverters.ObjectValueConverter.Convert(objectValue, convertToType);

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
		return Name.Equals(variableName.Replace("%", ""), StringComparison.OrdinalIgnoreCase);
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
		if (Value is JValue jValue)
		{
			if (jValue.Value is string valueStr)
			{
				var str2 = obj.ToString();
				return string.Equals(valueStr, str2, stringComparison ?? StringComparison.OrdinalIgnoreCase);
			}
			return jValue.Value?.Equals(obj) == true;
		}
		return Value.Equals(obj);
	}

	public static ObjectValue Nullable(string? variableName, bool initiated = false)
	{
		return new ObjectValue(variableName ?? String.Empty, null, typeof(Nullable), null, initiated);
	}
}
