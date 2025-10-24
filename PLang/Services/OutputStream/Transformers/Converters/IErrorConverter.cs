using Newtonsoft.Json;
using PLang.Errors;
using PLang.Errors.Runtime;
using PLang.Utils;
using System.Text.Json;

namespace PLang.Services.OutputStream.Transformers.Converters;

public class IErrorConverter : System.Text.Json.Serialization.JsonConverter<IError>
{
	public override IError Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
		=> throw new NotImplementedException();

	public override void Write(Utf8JsonWriter writer, IError value, JsonSerializerOptions options)
	{
		writer.WriteStartObject();
		writer.WriteString("message", value.Message);
		writer.WriteString("details", ErrorHelper.ToFormat("text", value).ToString());
		writer.WriteString("key", value.Key);
		writer.WriteString("type", value.GetType().Name);
		if (value is UserInputError uie)
		{
			writer.WriteString("callback", JsonConvert.SerializeObject(uie.Callback).ToBase64());

		}
		writer.WriteString("statusCode", value.StatusCode.ToString());
		writer.WriteEndObject();
	}
}