using PLang.Errors;
using PLang.Interfaces;
using PLang.Services.OutputStream.Messages;
using System.Buffers;
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

	public override async Task<(long, IError?)> Transform(PLangContext context, PipeWriter writer, OutMessage data, CancellationToken ct = default)
	{
		var env = TransformerHelper.BuildEnvelope(data, context);

		long length = 0;
		SemaphoreSlim? gate = null;

		try
		{
			gate = await TransformerHelper.GetGate(context.SharedItems, ct);
			/*
			using (var jsonWriter = new Utf8JsonWriter(writer, _writerOptions))
			{

				gate = await TransformerHelper.GetGate(context.SharedItems, ct);

				JsonSerializer.Serialize(jsonWriter, env, _opts);
				length = jsonWriter.BytesCommitted;
			}

			var nl = writer.GetSpan(1);
			nl[0] = (byte)'\n';
			writer.Advance(1);	*/
			var bufferWriter = new ArrayBufferWriter<byte>();
			using (var jsonWriter = new Utf8JsonWriter(bufferWriter, _writerOptions))
			{
				JsonSerializer.Serialize(jsonWriter, env, _opts);
				length = jsonWriter.BytesCommitted;
			} 

			
			var jsonBytes = bufferWriter.WrittenSpan;
			var span = writer.GetSpan(jsonBytes.Length + 1);
			jsonBytes.CopyTo(span);
			span[jsonBytes.Length] = (byte)'\n';
			writer.Advance(jsonBytes.Length + 1);
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
