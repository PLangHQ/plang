using Newtonsoft.Json;
using System.Globalization;

namespace PLang.Utils.JsonConverters
{
	// Confidence used to be the llm's word ("High", "Medium", "Low") and is now a 0-1 number from
	// the decider. Old .pr files still carry the word, so reading accepts both and writing always
	// emits the number.
	public class ConfidenceConverter : JsonConverter<double?>
	{
		public static double? Parse(string? confidence)
		{
			if (string.IsNullOrWhiteSpace(confidence)) return null;
			switch (confidence.Trim().ToLowerInvariant())
			{
				case "very high": return 1.0;
				case "high": return 0.9;
				case "medium": return 0.5;
				case "low": return 0.2;
				case "very low": return 0.1;
			}
			return double.TryParse(confidence, NumberStyles.Any, CultureInfo.InvariantCulture, out var value) ? value : null;
		}

		public override double? ReadJson(JsonReader reader, Type objectType, double? existingValue, bool hasExistingValue, JsonSerializer serializer)
		{
			if (reader.TokenType == JsonToken.Null) return null;
			if (reader.TokenType == JsonToken.Float || reader.TokenType == JsonToken.Integer) return Convert.ToDouble(reader.Value, CultureInfo.InvariantCulture);
			if (reader.TokenType == JsonToken.String) return Parse(reader.Value?.ToString());
			return null;
		}

		public override void WriteJson(JsonWriter writer, double? value, JsonSerializer serializer)
		{
			if (value == null) writer.WriteNull();
			else writer.WriteValue(value.Value);
		}
	}
}
