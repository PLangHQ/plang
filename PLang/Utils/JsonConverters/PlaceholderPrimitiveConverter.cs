
using System;
using System.Collections.Generic;
using System.Globalization;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
namespace PLang.Utils.JsonConverters;


public sealed class PlaceholderPrimitiveConverter : JsonConverter
{
	static readonly HashSet<Type> Supported = new()
	{
		typeof(bool),  typeof(bool?),
		typeof(byte),  typeof(byte?),
		typeof(sbyte), typeof(sbyte?),
		typeof(short), typeof(short?),
		typeof(ushort),typeof(ushort?),
		typeof(int),   typeof(int?),
		typeof(uint),  typeof(uint?),
		typeof(long),  typeof(long?),
		typeof(ulong), typeof(ulong?),
		typeof(float), typeof(float?),
		typeof(double),typeof(double?),
		typeof(decimal),typeof(decimal?)
	};

	public override bool CanConvert(Type objectType) => Supported.Contains(objectType);

	public override object? ReadJson(JsonReader reader, Type objectType, object? existingValue, JsonSerializer serializer)
	{
		var token = JToken.Load(reader);
		var target = Nullable.GetUnderlyingType(objectType) ?? objectType;

		if (token.Type == JTokenType.String)
		{
			var s = token.Value<string>() ?? string.Empty;
			if (IsPlaceholder(s)) return IsNullable(objectType) ? null : GetDefault(target);
			return ParseString(target, s, out var parsed) ? parsed : GetDefault(target);
		}

		try { return token.ToObject(objectType); }
		catch { return IsNullable(objectType) ? null : GetDefault(target); }
	}

	public override void WriteJson(JsonWriter writer, object? value, JsonSerializer serializer)
		=> serializer.Serialize(writer, value);

	static bool IsNullable(Type t) => Nullable.GetUnderlyingType(t) != null;

	static bool IsPlaceholder(string s)
		=> s.Length >= 2 && s[0] == '%' && s[^1] == '%';

	static object GetDefault(Type t) => t.IsValueType ? Activator.CreateInstance(t)! : null!;

	static bool ParseString(Type t, string s, out object? result)
	{
		var style = NumberStyles.Float | NumberStyles.AllowThousands;
		var ci = CultureInfo.InvariantCulture;
		result = null;

		switch (Type.GetTypeCode(t))
		{
			case TypeCode.Boolean: if (bool.TryParse(s, out var b)) { result = b; return true; } break;
			case TypeCode.Byte: if (byte.TryParse(s, style, ci, out var b1)) { result = b1; return true; } break;
			case TypeCode.SByte: if (sbyte.TryParse(s, style, ci, out var sb)) { result = sb; return true; } break;
			case TypeCode.Int16: if (short.TryParse(s, style, ci, out var i16)) { result = i16; return true; } break;
			case TypeCode.UInt16: if (ushort.TryParse(s, style, ci, out var ui16)) { result = ui16; return true; } break;
			case TypeCode.Int32: if (int.TryParse(s, style, ci, out var i32)) { result = i32; return true; } break;
			case TypeCode.UInt32: if (uint.TryParse(s, style, ci, out var ui32)) { result = ui32; return true; } break;
			case TypeCode.Int64: if (long.TryParse(s, style, ci, out var i64)) { result = i64; return true; } break;
			case TypeCode.UInt64: if (ulong.TryParse(s, style, ci, out var ui64)) { result = ui64; return true; } break;
			case TypeCode.Single: if (float.TryParse(s, style, ci, out var f)) { result = f; return true; } break;
			case TypeCode.Double: if (double.TryParse(s, style, ci, out var d)) { result = d; return true; } break;
			case TypeCode.Decimal: if (decimal.TryParse(s, style, ci, out var m)) { result = m; return true; } break;
		}
		return false;
	}

}