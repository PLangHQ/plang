using Newtonsoft.Json.Linq;
using ReverseMarkdown.Converters;
using System;
using System.Text;
using System.Text.RegularExpressions;

public static class JObjectVarResolver
{
	static readonly Regex Placeholder = new("%(?<name>[^%]+)%", RegexOptions.Compiled);

	public static void ResolvePlaceholders(this JToken token, Func<string, object?> resolve)
	{
		switch (token.Type)
		{
			case JTokenType.Object:
				var properties = ((JObject)token).Properties();
				for (int i=0;i< properties.Count();i++)
				{
					ResolvePlaceholders(properties.ElementAt(i).Value, resolve);
				}
					
				break;

			case JTokenType.Array:
				var jArray = (JArray)token;
				for (int i = 0; i < jArray.Count; i++)
				{
					ResolvePlaceholders(jArray[i], resolve);
				}
				break;

			case JTokenType.String:
				var s = token.Value<string>()!;
				var matches = Placeholder.Matches(s);
				if (matches.Count == 0) return;

				if (matches.Count == 1 && matches[0].Value == s)
				{
					var name = matches[0].Groups["name"].Value;
					var value = resolve(name);
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
						var v = resolve(name);
						return v?.ToString() ?? "";
					});
					token.Replace(new JValue(replaced));
				}
				break;
		}
	}
}
