using HtmlAgilityPack;
using Microsoft.AspNetCore.Mvc.TagHelpers;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using PLang.Errors;
using PLang.Models;
using PLang.Models.ObjectValueConverters;
using PLang.Models.ObjectValueExtractors;
using PLang.Utils;
using System.Collections;
using System.Diagnostics;
using System.Globalization;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
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
		get
		{
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
		if (string.IsNullOrEmpty(name) && value.ToString().Equals(""))
		{
			name = "EmptyString";
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

			int counter = 0;
			while (parent.Parent != null)
			{
				parent = parent.Parent;
				if (counter++ > 100)
				{
					Console.WriteLine($"To deep: ObjectValue.Root - goalName:{Name}");
					break;
				}
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
			if (!this.Initiated) this.Created = DateTime.Now;

			this.Initiated = true;
			this.Type = (value != null) ? value.GetType() : typeof(Nullable);


			if (Parent != null) SetParent(value);
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
	private JToken GetJToken(object? value) => value switch
	{
		null => JValue.CreateNull(),
		JToken jt => jt,
		_ => JToken.FromObject(value)
	};

	private void SetParent(object? value)
	{
		var parentValue = Parent!.Value;
		if (parentValue is JObject jobj)
		{
			var property = jobj.Properties().FirstOrDefault(p => p.Name.Equals(Name, StringComparison.OrdinalIgnoreCase));
			JToken jToken = (value is JToken jt) ? jt : GetJToken(value);

			if (property == null)
			{
				jobj.Add(Name, jToken);
			}
			else
			{
				property.Value = jToken;
			}
			Parent.Value = jobj;
		}
		else if (parentValue is JArray jArray)
		{
			JToken jToken = (value is JToken jt) ? jt : GetJToken(value); ;

			if (Name.Contains("[") && Name.Contains("]"))
			{
				if (int.TryParse(Name.Trim('[').Trim(']'), out int index))
				{
					jArray[index] = jToken;
				} else
				{
					throw new Exception($"{Name} is not supported on index. {ErrorReporting.CreateIssueNotImplemented}");
				}
			}
			else
			{
				try
				{
					foreach (var item in jArray.OfType<JObject>())
					{
						if (item[Name] == null) continue;
						item[Name] = null;
					}
				} catch (Exception ex)
				{
					Console.WriteLine($"Name:{Name} | JToken:{jToken}");
					Console.WriteLine(ex);
				}
			}

			Parent.Value = jArray;
		}
		else if (parentValue == null)
		{
			JToken jToken = (value is JToken jt) ? jt : GetJToken(value);
			JObject newObj = new JObject();
			newObj[Name] = jToken;

			Parent!.Value = newObj;

		}
		else
		{
			object? parentReference = Parent;
			if (Parent is ObjectValue ov)
			{
				parentReference = ov.Value;
			}

			if (TypeHelper.ImplementsDict(parentReference, out var dict))
			{
				if (dict.Contains(Name))
				{
					dict[Name] = value;
				}
				else
				{
					dict.Add(Name, value);
				}

				Parent.Value = dict;
			}
			else
			{

				throw new NotImplementedException($"The type {parentValue.GetType()} is not yet implemented. {ErrorReporting.CreateIssueNotImplemented}");
			}
		}

	}
	public Type? Type { get; set; }
	public bool Initiated { get; set; }
	[JsonIgnore]
	public ObjectValue? Parent
	{
		get { return parent; }
		set
		{
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
	public virtual object? ValueAs(Type convertToType)
	{
		return Models.ObjectValueConverters.ObjectValueConverter.Convert(this, convertToType);

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
			var str2 = Convert.ToString(obj);
			return string.Equals(str, str2, stringComparison ?? StringComparison.OrdinalIgnoreCase);
		}
		if (Value is JValue jValue)
		{
			if (jValue.Value is string valueStr)
			{
				var str2 = Convert.ToString(obj);
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

	public void Set(string path, ObjectValue childObjectValue)
	{
		ObjectPath.Set(this, path, childObjectValue);
	}
}

public static class ObjectPath
{
	public static void Set(ObjectValue root, string path, object? newValue)
	{
		var tokens = Parse(path);
		if (tokens.Count == 0) throw new ArgumentException("Empty path.", nameof(path));

		var i = 0;
		if (tokens[0] is KeyToken k0 && string.Equals(k0.Name, root.Path, StringComparison.Ordinal)) i++;
		if (i >= tokens.Count) { root.Value = newValue; return; }

		root.Value = EnsureContainer(root.Value, tokens[i]);
		var cur = root.Value;

		for (; i < tokens.Count; i++)
		{
			var last = i == tokens.Count - 1;
			var t = tokens[i];
			var next = last ? null : tokens[i + 1];

			if (t is KeyToken kt)
			{
				if (cur is IDictionary<string, object?> d)
				{
					if (!d.TryGetValue(kt.Name, out var child) || child == null || (!last && !IsContainer(child)))
					{
						if (last) { d[kt.Name] = newValue; return; }
						d[kt.Name] = CreateContainer(next);
					}
					if (last) { d[kt.Name] = newValue; return; }
					d[kt.Name] = EnsureContainer(d[kt.Name], next!);
					cur = d[kt.Name];
				}
				else if (cur is IDictionary nd)
				{
					var child = nd.Contains(kt.Name) ? nd[kt.Name] : null;
					if (child == null || (!last && !IsContainer(child)))
					{
						if (last) { nd[kt.Name] = newValue; return; }
						nd[kt.Name] = CreateContainer(next);
					}
					if (last) { nd[kt.Name] = newValue; return; }
					nd[kt.Name] = EnsureContainer(nd[kt.Name], next!);
					cur = nd[kt.Name];
				}
				else throw new InvalidOperationException($"Segment '{kt.Name}' requires a dictionary.");
			}
			else if (t is IndexToken ix)
			{
				if (cur is IList list)
				{
					EnsureSize(list, ix.Index + 1);
					var child = list[ix.Index];
					if (child == null || (!last && !IsContainer(child)))
					{
						if (last) { list[ix.Index] = newValue; return; }
						list[ix.Index] = CreateContainer(next);
					}
					if (last) { list[ix.Index] = newValue; return; }
					list[ix.Index] = EnsureContainer(list[ix.Index], next!);
					cur = list[ix.Index];
				}
				else throw new InvalidOperationException($"Index {ix.Index} requires a list.");
			}
		}
	}

	static object CreateContainer(Token? next) =>
		next is IndexToken ? new List<object?>() : new Dictionary<string, object?>(StringComparer.Ordinal);

	static object EnsureContainer(object? o, Token first)
	{
		if (IsContainer(o)) return o!;
		if (o is null) return CreateContainer(first);
		return MaterializeToMutable(o, first);
	}

	static bool IsContainer(object? o) => o is IDictionary || o is IList;

	static void EnsureSize(IList list, int size) { while (list.Count < size) list.Add(null); }

	// --- Anonymous/POCO support ---
	static object MaterializeToMutable(object o, Token next)
	{
		// Arrays -> List<object?>
		if (o is Array arr) return ArrayToList(arr);
		// Any other object -> Dictionary<string, object?>
		return ObjectToDictionary(o);
	}

	static IList ArrayToList(Array a)
	{
		var list = new List<object?>(a.Length);
		foreach (var e in a) list.Add(CloneValue(e));
		return list;
	}

	static Dictionary<string, object?> ObjectToDictionary(object o)
	{
		var dict = new Dictionary<string, object?>(StringComparer.Ordinal);
		var t = o.GetType();
		foreach (var p in t.GetProperties(BindingFlags.Instance | BindingFlags.Public))
		{
			if (p.GetMethod is null || p.GetIndexParameters().Length != 0) continue;
			var v = p.GetValue(o);
			dict[p.Name] = CloneValue(v);
		}
		return dict;
	}

	static object? CloneValue(object? v)
	{
		if (v is null) return null;
		if (IsPrimitiveLike(v)) return v;
		if (v is IDictionary<string, object?> d1)
			return d1.ToDictionary(kv => kv.Key, kv => CloneValue(kv.Value), StringComparer.Ordinal);
		if (v is IDictionary d2)
		{
			var m = new Dictionary<string, object?>(StringComparer.Ordinal);
			foreach (DictionaryEntry e in d2) m[Convert.ToString(e.Key, CultureInfo.InvariantCulture)!] = CloneValue(e.Value);
			return m;
		}
		if (v is Array a) return ArrayToList(a);
		if (v is IList l)
		{
			var n = new List<object?>(l.Count);
			foreach (var e in l) n.Add(CloneValue(e));
			return n;
		}
		return ObjectToDictionary(v); // anonymous/POCO
	}

	static bool IsPrimitiveLike(object v)
	{
		if (v is string or DateTime or DateTimeOffset or Guid or decimal) return true;
		var tc = Type.GetTypeCode(v.GetType());
		return tc is >= TypeCode.Boolean and <= TypeCode.Double;
	}

	public static List<Token> Parse(string path)
	{
		if (string.IsNullOrWhiteSpace(path)) throw new ArgumentException("path");

		path = path.Replace("%", "");
		var tokens = new List<Token>();
		var sb = new StringBuilder();

		for (int i = 0; i < path.Length; i++)
		{
			var c = path[i];
			if (c == '.')
			{
				if (sb.Length > 0) { tokens.Add(new KeyToken(sb.ToString())); sb.Clear(); }
				continue;
			}
			if (c == '[')
			{
				if (sb.Length > 0) { tokens.Add(new KeyToken(sb.ToString())); sb.Clear(); }
				int j = ++i, start = j;
				while (j < path.Length && path[j] != ']')
				{
					if (!char.IsDigit(path[j])) throw new FormatException("Non-numeric index.");
					j++;
				}
				if (j >= path.Length) throw new FormatException("Missing ']'.");
				var num = path.Substring(start, j - start);
				if (num.Length == 0) throw new FormatException("Empty index.");
				tokens.Add(new IndexToken(int.Parse(num, CultureInfo.InvariantCulture)));
				i = j;
				continue;
			}
			sb.Append(c);
		}
		if (sb.Length > 0) tokens.Add(new KeyToken(sb.ToString()));
		return tokens;
	}

	public abstract record Token;
	public sealed record KeyToken(string Name) : Token;
	public sealed record IndexToken(int Index) : Token;

	public static bool IsAnonymousType(Type t) =>
		Attribute.IsDefined(t, typeof(CompilerGeneratedAttribute)) &&
		t.IsGenericType && t.Name.Contains("AnonymousType", StringComparison.Ordinal) &&
		(t.Attributes & TypeAttributes.NotPublic) == TypeAttributes.NotPublic;
}
