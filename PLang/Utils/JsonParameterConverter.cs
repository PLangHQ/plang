using NBitcoin;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using ReverseMarkdown.Converters;
using System;
using System.Text;
using System.Text.RegularExpressions;

/// <summary>
/// What a JObject parameter of a step contains, found once instead of on every run: whether it mentions
/// "%Settings." and where its string tokens with %placeholders% are. The JObject comes from the Instruction
/// in the shared goal graph and is never changed after it is loaded (every run works on a DeepClone), so the
/// answer is the same for every run. A plan is made for one parameter type, because the type decides the
/// target type that is handed to the resolver for each token.
/// </summary>
public sealed class JObjectPlan
{
	/// <summary>One string token that has at least one %placeholder%: where it is (property names and array
	/// indexes from the root), its text, the target type the resolver gets, and whether the whole text is one placeholder.</summary>
	public sealed record Entry(object[] Path, string Text, Type? TargetType, bool Single);

	public Type AnalyzedFor { get; }
	public bool HasSettings { get; }
	public Entry[] Entries { get; }

	private JObjectPlan(Type analyzedFor, bool hasSettings, Entry[] entries)
	{
		AnalyzedFor = analyzedFor;
		HasSettings = hasSettings;
		Entries = entries;
	}

	private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<JObject, JObjectPlan> plans = new();

	/// <summary>The plan of this JObject for the parameter type, or null when it cannot be analysed (the caller
	/// then does what it did before the plan existed, which includes throwing the same exceptions).</summary>
	public static JObjectPlan? Get(JObject jobj, Type parameterType)
	{
		if (plans.TryGetValue(jobj, out var existing) && existing.AnalyzedFor == parameterType) return existing;

		try
		{
			var entries = new List<Entry>();
			Walk(jobj, parameterType, new List<object>(), entries);
			var plan = new JObjectPlan(parameterType, jobj.ToString().Contains("%Settings."), entries.ToArray());
			plans.AddOrUpdate(jobj, plan);
			return plan;
		}
		catch (Exception)
		{
			return null;
		}
	}

	// The same walk as JObjectVarResolver.ResolvePlaceholders, which decides the target type of every token.
	private static void Walk(JToken token, Type targetType, List<object> path, List<Entry> entries)
	{
		switch (token.Type)
		{
			case JTokenType.Object:
				foreach (var prop in ((JObject)token).Properties())
				{
					var propInfo = targetType?.GetProperty(prop.Name);
					var propType = propInfo?.PropertyType ?? typeof(object);
					path.Add(prop.Name);
					Walk(prop.Value, propType ?? targetType, path, entries);
					path.RemoveAt(path.Count - 1);
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
					path.Add(i);
					Walk(jArray[i], type, path, entries);
					path.RemoveAt(path.Count - 1);
				}
				break;

			case JTokenType.String:
				var s = token.Value<string>()!;
				var matches = JObjectVarResolver.Placeholder.Matches(s);
				if (matches.Count == 0) return;
				entries.Add(new Entry(path.ToArray(), s, targetType, matches.Count == 1 && matches[0].Value == s));
				break;
		}
	}
}

public static class JObjectVarResolver
{
	internal static readonly Regex Placeholder = new("%(?<name>[^%]+)%", RegexOptions.Compiled);
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
				ResolveString(token, token.Value<string>()!, targetType, resolve);
				break;
		}
	}

	/// <summary>
	/// Resolves only the tokens the plan found (the tokens that have a %placeholder%), on a clone of the
	/// JObject the plan was made from, instead of walking and regex matching every token. The clone has the same
	/// shape as the original, so the recorded paths lead to the same tokens, and each token gets the same
	/// target type and the same treatment as in <see cref="ResolvePlaceholders"/>, in the same order.
	/// </summary>
	public static void ResolvePlaceholders(this JToken clone, JObjectPlan plan, Func<string, Type?, object?> resolve)
	{
		foreach (var entry in plan.Entries)
		{
			JToken token = clone;
			foreach (var segment in entry.Path)
			{
				token = segment is string name ? ((JObject)token)[name]! : ((JArray)token)[(int)segment];
			}
			ResolveString(token, entry.Text, entry.TargetType, resolve);
		}
	}

	private static void ResolveString(JToken token, string s, Type? targetType, Func<string, Type?, object?> resolve)
	{
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
	}
}
