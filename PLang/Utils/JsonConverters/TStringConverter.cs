using Newtonsoft.Json;

namespace PLang.Utils.JsonConverters;
public class TStringConverter : JsonConverter<TString>
{
	private readonly Dictionary<string, string> _translation;
	private readonly dynamic _memoryStack;

	public TStringConverter(Dictionary<string, string> translation, dynamic memoryStack)
	{
		_translation = translation ?? throw new ArgumentNullException(nameof(translation));
		_memoryStack = memoryStack ?? throw new ArgumentNullException(nameof(memoryStack));
	}

	public override TString ReadJson(JsonReader reader, Type objectType, TString existingValue,
									 bool hasExistingValue, JsonSerializer serializer)
	{
		if (reader.TokenType == JsonToken.Null)
			return null;

		string value = reader.Value?.ToString();
		return new TString(value);
	}

	public override void WriteJson(JsonWriter writer, TString value, JsonSerializer serializer)
	{
		writer.WriteValue(value?.ToString());
	}
}