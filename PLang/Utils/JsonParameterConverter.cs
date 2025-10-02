using Newtonsoft.Json.Linq;
using ReverseMarkdown.Converters;
using System;
using System.Text;
using System.Text.RegularExpressions;

public static class JObjectVarResolver
{
	static readonly Regex Placeholder = new("%(?<name>[^%]+)%", RegexOptions.Compiled);

	public static void ResolvePlaceholders(this JToken token, Type targetType, Func<string, Type?, object?> resolve)
	{
		switch (token.Type)
		{
			case JTokenType.Object:
				var properties = ((JObject)token).Properties();
				foreach (var prop in properties)
				{
					var propInfo = targetType?.GetProperty(prop.Name);
					var propType = propInfo?.PropertyType ?? typeof(object);
					ResolvePlaceholders(prop.Value, propType ?? targetType, resolve);
				}

				break;

			case JTokenType.Array:
				var jArray = (JArray)token;
				for (int i = 0; i < jArray.Count; i++)
				{
					var type = targetType ?? typeof(object);
					if (type.GenericTypeArguments.Length > 0)
					{
						type = type.GenericTypeArguments[0];
					}
					ResolvePlaceholders(jArray[i], type, resolve);
				}
				break;

			case JTokenType.String:
				var s = token.Value<string>()!;
				var matches = Placeholder.Matches(s);
				if (matches.Count == 0) return;

				if (matches.Count == 1 && matches[0].Value == s)
				{
					var name = matches[0].Groups["name"].Value;
					var value = resolve(name, targetType);
					JToken newTok = value switch
					{
						null => JValue.CreateNull(),
						JToken jt => jt,
						_ => JToken.FromObject(value)
					};
					token.Replace(newTok);
				}
				else
				{
					var replaced = Placeholder.Replace(s, m =>
					{
						var name = m.Groups["name"].Value;
						var v = resolve(name, targetType);
						return v?.ToString() ?? "";
					});
					token.Replace(new JValue(replaced));
				}
				break;
		}
	}
}
