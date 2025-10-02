using Microsoft.AspNetCore.Http;
using Parlot.Fluent;
using PLang.Errors;
using PLang.Errors.Runtime;
using PLang.Interfaces;
using PLang.Services.OutputStream.Messages;
using PLang.Utils;
using System.Diagnostics;
using System.IO.Pipelines;
using System.Text;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace PLang.Services.OutputStream.Transformers;


public class TextTransformer : ITransformer
{
	protected readonly Encoding encoding;

	public TextTransformer(Encoding encoding)
	{
		this.encoding = encoding;
	}
	public Encoding Encoding { get { return encoding; } }

	public virtual string ContentType { get { return "plain/text"; } }

	public async Task<(long, IError?)> Transform(PLangContext context, PipeWriter writer, OutMessage obj, CancellationToken ct = default)
	{
		if (obj == null) return (0, null);


		SemaphoreSlim? gate = null;
		try
		{
			int needed = 0;
			Span<byte> span;
			int length = 0;

			if (obj is TextMessage tm)
			{
				needed = encoding.GetByteCount(tm.Content);

				gate = await TransformerHelper.GetGate(context.SharedItems, ct);
				span = writer.GetSpan(needed);

				
				length = encoding.GetBytes(tm.Content.AsSpan(), span);
				writer.Advance(length);

				return (length, null);

			}

			if (obj is RenderMessage rm)
			{
				needed = encoding.GetByteCount(rm.Content);
				gate = await TransformerHelper.GetGate(context.SharedItems, ct);
				span = writer.GetSpan(needed);

				
				length = encoding.GetBytes(rm.Content.AsSpan(), span);

				writer.Advance(length);

				return (length, null);

			}

			string? content = StringHelper.ConvertToString(obj);
			if (content == null) return (0, null);

			needed = encoding.GetByteCount(content);
			gate = await TransformerHelper.GetGate(context.SharedItems, ct);
			span = writer.GetSpan(needed);

			
			length = encoding.GetBytes(content.AsSpan(), span);
			return (length, null);

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

