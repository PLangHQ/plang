using NBitcoin.Protocol;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Serialization;
using PLang.Errors;
using PLang.Interfaces;
using PLang.Services.OutputStream.Messages;
using PLang.Services.OutputStream.Transformers.Converters;
using PLang.Utils.JsonConverters;
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

	JsonSerializerSettings _newtonsoftSettings = new JsonSerializerSettings
	{
		ContractResolver = new CamelCasePropertyNamesContractResolver(),
		Formatting = Formatting.None,
		Converters = { new StringEnumConverter(), new NewtonsoftObjectValueConverter(), new LongAsStringConverter() }
	};

	public override async Task<(long, IError?)> Transform(PLangContext context, PipeWriter writer, OutMessage data, CancellationToken ct = default)
	{
		var (env, error) = TransformerHelper.BuildEnvelope(data, context);
		if (error != null) return (0, error);

		long length = 0;
		SemaphoreSlim? gate = null;

		try
		{
			gate = await TransformerHelper.GetGate(context.SharedItems, ct);

			var json = JsonConvert.SerializeObject(env, _newtonsoftSettings);
			var jsonBytes = Encoding.UTF8.GetBytes(json);
			length = jsonBytes.Length;
			
			// Write to PipeWriter
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
