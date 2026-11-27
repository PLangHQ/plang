using System.Collections.Concurrent;

namespace PLang.Utils
{
	/// <summary>
	/// Values that must never be shown: what a setting named as a secret gave (%Settings.ApiKey%, a token) and what a
	/// step marked as sensitive.
	/// The value itself is kept, not the variable that held it, so a copy (`set %t% = %Settings.Token%`) or a value built
	/// from it (`"Bearer %t%"`) is hidden too. Modules get the real value; text that goes out to be read (an error and
	/// its variables, the log, a request to the llm) is passed through Redact, which shows **** and the last 4 characters.
	/// What the app writes out (write out, render, files, http) is its own output and gets the value as it is.
	/// Values shorter than 6 characters are not kept: hiding them would hide ordinary words and numbers.
	/// </summary>
	public static class SensitiveValues
	{
		private const int MinimumLength = 6;
		private static readonly ConcurrentDictionary<string, byte> values = new();
		private static string[] ordered = [];
		private static readonly object orderLock = new();

		public static int Count => values.Count;

		private static readonly System.Text.RegularExpressions.Regex SecretName = new(
			"key|token|secret|password",
			System.Text.RegularExpressions.RegexOptions.IgnoreCase | System.Text.RegularExpressions.RegexOptions.Compiled);

		/// <summary>
		/// A setting whose name says it is a secret (ApiKey, SmtpPassword, GithubToken, ClientSecret). Only these are
		/// sensitive: a setting such as SiteDomain or AppPath is app data.
		/// </summary>
		public static bool IsSecretName(string? name) => !string.IsNullOrEmpty(name) && SecretName.IsMatch(name);

		public static void Add(object? value)
		{
			var text = value switch
			{
				null => null,
				string s => s,
				Newtonsoft.Json.Linq.JValue jv => jv.Value?.ToString(),
				_ when value.GetType().IsPrimitive => value.ToString(),
				_ => null
			};
			if (string.IsNullOrEmpty(text) || text.Length < MinimumLength) return;
			if (!values.TryAdd(text, 0)) return;
			lock (orderLock)
			{
				// longest first, so a value that contains another is hidden whole
				ordered = values.Keys.OrderByDescending(p => p.Length).ToArray();
			}
		}

		public static bool IsSensitive(object? value)
		{
			return value is string s && values.ContainsKey(s);
		}

		/// <summary>The text with every sensitive value in it hidden.</summary>
		public static string? Redact(string? text)
		{
			if (string.IsNullOrEmpty(text) || values.IsEmpty) return text;
			var current = ordered;
			foreach (var value in current)
			{
				if (text.Contains(value, StringComparison.Ordinal)) text = text.Replace(value, Mask(value), StringComparison.Ordinal);
			}
			return text;
		}

		public static string Mask(string value)
		{
			return value.Length >= 12 ? "****" + value[^4..] : "****";
		}
	}
}
