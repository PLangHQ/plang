using PLang.Errors;
using PLang.Services.OutputStream.Messages;
using System.Diagnostics;
using System.IO;
using System.IO.Pipelines;
using System.Text;
using System.Text.Json;

namespace PLang.Services.OutputStream.Transformers;

public class PlangTransformer : JsonTransformer
{
	JsonTransformer jsonTransformer;

	public PlangTransformer(Encoding encoding) : base(encoding)
	{
		jsonTransformer = new JsonTransformer(encoding);
	}
	public override string ContentType { get { return "application/x-ndjson"; } }

	public static readonly object GateKey = new();

	public override async Task<(long, IError?)> Transform(HttpContext httpContext, PipeWriter writer, OutMessage data, CancellationToken ct = default)
	{
		var env = TransformerHelper.BuildEnvelope(data);

		long length = 0;
		SemaphoreSlim? gate = null;

		try
		{
			using var jsonWriter = new Utf8JsonWriter(writer, _writerOptions);

			gate = await TransformerHelper.GetGate(httpContext, ct);

			JsonSerializer.Serialize(jsonWriter, env, _opts);
			length = jsonWriter.BytesCommitted;

			var nl = writer.GetSpan(1);
			nl[0] = (byte)'\n';
			writer.Advance(1);		
		}
		catch (Exception ex)
		{
			return (0, new Error(ex.Message, Exception: ex, Data: env));
		}
		finally
		{
			gate?.Release();
		}

		return (length + 1, null);


	}


}
