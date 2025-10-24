using Newtonsoft.Json;
using PLang.Runtime;
using System.Text.Json;

namespace PLang.Services.OutputStream.Transformers.Converters;
public class ObjectValueConverter : System.Text.Json.Serialization.JsonConverter<ObjectValue>
{
	public override ObjectValue Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
		=> throw new NotImplementedException();

	public override void Write(Utf8JsonWriter writer, ObjectValue value, JsonSerializerOptions options)
	{
		System.Text.Json.JsonSerializer.Serialize(writer, value.Value, options);
	}
}


public class NewtonsoftObjectValueConverter : JsonConverter
{
	public override bool CanConvert(Type objectType)
	{
		return objectType == typeof(ObjectValue); // Your ObjectValue type
	}

	public override object ReadJson(JsonReader reader, Type objectType, object existingValue, Newtonsoft.Json.JsonSerializer serializer)
	{
		throw new NotImplementedException();
	}

	public override void WriteJson(JsonWriter writer, object value, Newtonsoft.Json.JsonSerializer serializer)
	{
		var objectValue = (ObjectValue)value;

		serializer.Serialize(writer, objectValue.Value);
	}
}

