using NBitcoin;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using ReverseMarkdown.Converters;
using System;
using System.Text;
using System.Text.RegularExpressions;

public static class JObjectVarResolver
{
	static readonly Regex Placeholder = new("%(?<name>[^%]+)%", RegexOptions.Compiled);
	static JsonSerializerSettings settings = new JsonSerializerSettings
	{
		Error = (sender, args) =>
		{
			// Set the property to null and continue
			args.ErrorContext.Handled = true;
		},
		ReferenceLoopHandling = ReferenceLoopHandling.Ignore
	};
	public static void ResolvePlaceholders(this JToken token, Type targetType, Func<string, Type?, object?> resolve)
	{
		switch (token.Type)
		{
			case JTokenType.Object:
				var properties = ((JObject)token).Properties();
				foreach (var prop in properties)
				{
					try
					{
						var propInfo = targetType?.GetProperty(prop.Name);
						var propType = propInfo?.PropertyType ?? typeof(object);
						ResolvePlaceholders(prop.Value, propType ?? targetType, resolve);
					} catch (Exception ex)
					{
						Console.WriteLine($"Modified collection: {prop.Name} | {prop.Value}");
						throw;
					}
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
					string? name = null;
					object? value = null;
					try
					{
						name = matches[0].Groups["name"].Value;
						value = resolve(name, targetType);
						JToken newTok = value switch
						{
							null => JValue.CreateNull(),
							JToken jt => jt,
							_ => JToken.FromObject(value, JsonSerializer.Create(settings))
						};
						token.Replace(newTok);
					} catch (Exception ex)
					{
						int i = 0;
						Console.WriteLine($"JsonResolve on '{name}' - value:{value} - error:{ex.Message}");
						
					}
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
