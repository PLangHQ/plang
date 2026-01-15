


using System;
using System.Collections;
using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace PLang.Utils.JsonConverters;

public sealed class PlaceholderCollectionConverter : JsonConverter
{
	public override bool CanConvert(Type objectType)
	{
		if (typeof(IDictionary).IsAssignableFrom(objectType)) return true;
		if (typeof(IList).IsAssignableFrom(objectType)) return true;
		if (!objectType.IsGenericType) return false;

		return IsGenericTypeDefintion(objectType);
	}

	private bool IsGenericTypeDefintion(Type objectType)
	{
		var generic = objectType.GetGenericTypeDefinition();
		return TypeHelper.IsGenericListTypeDefintion(generic) || TypeHelper.IsGenericDictTypeDefintion(generic);
	}
	
	public override object? ReadJson(JsonReader reader, Type objectType, object? existingValue, JsonSerializer serializer)
	{
		var token = JToken.Load(reader);
		if (token is JObject jobject)
		{
			return jobject.ToDictionary();
		}
		if (token is JArray jArray && TypeHelper.IsList(objectType))
		{
			return TypeHelper.ConvertToList(objectType, jArray);
		}

		if (token.Type == JTokenType.String)
		{
			var s = token.Value<string>() ?? string.Empty;
			if (IsPlaceholder(s))
			{
				return CreateInstance(objectType);
			}
		}
		if (token is JValue jValue && jValue.Value == null) return null;

		try { return token.ToObject(objectType, serializer); }
		catch { return CreateInstance(objectType); }
	}

	public override void WriteJson(JsonWriter writer, object? value, JsonSerializer serializer)
		=> serializer.Serialize(writer, value);

	static bool IsPlaceholder(string s)
		=> s.Length >= 2 && s[0] == '%' && s[^1] == '%';

	private object? CreateInstance(Type t)
	{
		if (t.IsInterface && t.IsGenericType)
		{
			var generic = t.GetGenericTypeDefinition();
			var args = t.GetGenericArguments();

			if (TypeHelper.IsGenericDictTypeDefintion(generic))
				return Activator.CreateInstance(typeof(Dictionary<,>).MakeGenericType(args));

			if (TypeHelper.IsGenericListTypeDefintion(generic))
				return Activator.CreateInstance(typeof(List<>).MakeGenericType(args));
		}

		return Activator.CreateInstance(t);
	}
}

