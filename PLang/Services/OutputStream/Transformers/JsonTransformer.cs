using PLang.Errors;
using PLang.Errors.Runtime;
using PLang.Interfaces;
using PLang.Services.OutputStream.Messages;
using System.IO.Pipelines;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace PLang.Services.OutputStream.Transformers;


public class JsonTransformer : ITransformer
{
	public virtual string ContentType => "application/json";
	public Encoding Encoding { get; }

	protected readonly JsonSerializerOptions _opts = new()
	{
		PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
		WriteIndented = false,
		Converters = { new JsonStringEnumConverter(), new ObjectValueConverter() }
	};

	protected readonly JsonWriterOptions _writerOptions = new()
	{

	};

	public JsonTransformer(Encoding enc) => Encoding = enc;

	public virtual async Task<(long, IError?)> Transform(PLangContext context, PipeWriter writer, OutMessage m, CancellationToken ct = default)
	{
		var env = TransformerHelper.BuildEnvelope(m, context);
		SemaphoreSlim? gate = null;
		try
		{
			long length = 0;
			using (var jsonWriter = new Utf8JsonWriter(writer, _writerOptions))
			{

				gate = await TransformerHelper.GetGate(context.SharedItems, ct);

				System.Text.Json.JsonSerializer.Serialize(jsonWriter, env, _opts);
				length = jsonWriter.BytesCommitted;
			}

			var nl = writer.GetSpan(1);
			nl[0] = (byte)'\n';
			writer.Advance(1);

			return (length + 1, null);
		}
		catch (Exception ex)
		{
			return (0, new ServiceError(ex.Message, GetType(), "TransformError", 500, Exception: ex));
		} finally
		{
			gate?.Release();
		}
	}
}

	

