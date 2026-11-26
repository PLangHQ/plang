using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using PLang.Errors;
using PLang.Errors.Runtime;
using PLang.Interfaces;
using PLang.Services.OutputStream.Messages;
using System.IO.Pipelines;
using System.Text;

namespace PLang.Services.OutputStream.Transformers;

/// <summary>
/// The answer of a route whose response content type is application/json: what the goal writes is the body, without
/// the message envelope of JsonTransformer. Json text (an object or list written out) goes as it is, other text as a
/// json string, an error as {"error", "key", "status"}.
/// </summary>
public class RawJsonTransformer : ITransformer
{
	public string ContentType => "application/json";
	public Encoding Encoding { get; }

	public RawJsonTransformer(Encoding encoding) => Encoding = encoding;

	public async Task<(long, IError?)> Transform(PLangContext context, PipeWriter writer, OutMessage? m, CancellationToken ct = default)
	{
		if (m == null || m is ExecuteMessage) return (0, null);

		string json = m switch
		{
			ErrorMessage em => JsonConvert.SerializeObject(new { error = em.Content, key = em.Key, status = em.StatusCode }),
			TextMessage tm => AsJson(tm.Content),
			RenderMessage rm => AsJson(rm.Content),
			AskMessage am => AsJson(am.Content),
			_ => AsJson(m.ToString())
		};

		SemaphoreSlim? gate = null;
		try
		{
			gate = await TransformerHelper.GetGate(context.SharedItems, ct);
			var needed = Encoding.GetByteCount(json);
			var span = writer.GetSpan(needed);
			var length = Encoding.GetBytes(json.AsSpan(), span);
			writer.Advance(length);
			return (length, null);
		}
		catch (Exception ex)
		{
			return (0, new ServiceError(ex.Message, GetType(), "TransformError", 500, Exception: ex));
		}
		finally
		{
			gate?.Release();
		}
	}

	private static string AsJson(string? content)
	{
		if (content == null) return "null";
		var trimmed = content.Trim();
		if (trimmed.StartsWith('{') || trimmed.StartsWith('['))
		{
			try
			{
				JToken.Parse(trimmed);
				return trimmed;
			}
			catch (JsonReaderException) { }
		}
		return JsonConvert.SerializeObject(content);
	}
}
