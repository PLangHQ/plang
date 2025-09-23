using PLang.Errors;
using PLang.Errors.Runtime;
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
		Converters = { new JsonStringEnumConverter() }
	};

	protected readonly JsonWriterOptions _writerOptions = new()
	{

	};

	public JsonTransformer(Encoding enc) => Encoding = enc;

	public virtual async Task<(long, IError?)> Transform(HttpContext httpContext, PipeWriter writer, OutMessage m, CancellationToken ct = default)
	{
		var env = TransformerHelper.BuildEnvelope(m);
		SemaphoreSlim? gate = null;
		try
		{
			using var jsonWriter = new Utf8JsonWriter(writer, _writerOptions);

			gate = await TransformerHelper.GetGate(httpContext, ct);

			System.Text.Json.JsonSerializer.Serialize(jsonWriter, env, _opts);
			long length = jsonWriter.BytesCommitted;
			jsonWriter.Flush();

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

	

