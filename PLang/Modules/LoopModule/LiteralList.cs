using Newtonsoft.Json.Linq;
using System.Globalization;

namespace PLang.Modules.LoopModule
{
	/// <summary>
	/// A list written straight into a loop step, e.g. `go through ["cpu","memory","io"], call goal X`.
	/// A value is a literal list when, after trimming, it starts with [ and ends with ]. No variable name can
	/// start with [ (MemoryStack.GetKeyPath rejects it), so this never takes over a %variable% reference.
	/// </summary>
	public static class LiteralList
	{
		public static bool TryParse(string? value, out List<object?> items)
		{
			items = new List<object?>();
			if (string.IsNullOrWhiteSpace(value)) return false;

			var text = value.Trim();
			if (text.Length < 2 || text[0] != '[' || text[^1] != ']') return false;

			JArray? array = null;
			try
			{
				array = JArray.Parse(text);
			}
			catch (Newtonsoft.Json.JsonException) { }

			if (array != null)
			{
				foreach (var token in array)
				{
					items.Add(token is JValue jValue ? jValue.Value : token);
				}
				return true;
			}

			// Not valid json, e.g. [cpu, memory, io]: the items are separated by commas
			var inner = text[1..^1];
			if (string.IsNullOrWhiteSpace(inner)) return true;

			foreach (var part in inner.Split(','))
			{
				items.Add(ParseItem(part.Trim()));
			}
			return true;
		}

		private static object? ParseItem(string item)
		{
			if (item.Length >= 2 && (item[0] == '"' || item[0] == '\'') && item[^1] == item[0])
			{
				return item[1..^1];
			}
			if (long.TryParse(item, NumberStyles.Integer, CultureInfo.InvariantCulture, out var l)) return l;
			if (double.TryParse(item, NumberStyles.Float, CultureInfo.InvariantCulture, out var d)) return d;
			if (bool.TryParse(item, out var b)) return b;
			return item;
		}
	}
}
