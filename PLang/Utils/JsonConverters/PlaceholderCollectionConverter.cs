


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

		var generic = objectType.GetGenericTypeDefinition();
		return generic == typeof(Dictionary<,>)
			|| generic == typeof(List<>)
			|| generic == typeof(IList<>)
			|| generic == typeof(ICollection<>)
			|| generic == typeof(IEnumerable<>)
			|| generic == typeof(IDictionary<,>);
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

		return CreateInstance(objectType);
	}

	public override void WriteJson(JsonWriter writer, object? value, JsonSerializer serializer)
		=> serializer.Serialize(writer, value);

	static bool IsPlaceholder(string s)
		=> s.Length >= 2 && s[0] == '%' && s[^1] == '%';

	static object? CreateInstance(Type t)
	{
		if (t.IsInterface && t.IsGenericType)
		{
			var generic = t.GetGenericTypeDefinition();
			var args = t.GetGenericArguments();

			if (generic == typeof(IDictionary<,>))
				return Activator.CreateInstance(typeof(Dictionary<,>).MakeGenericType(args));

			if (generic == typeof(IList<>) || generic == typeof(ICollection<>) || generic == typeof(IEnumerable<>))
				return Activator.CreateInstance(typeof(List<>).MakeGenericType(args));
		}

		return Activator.CreateInstance(t);
	}
}

