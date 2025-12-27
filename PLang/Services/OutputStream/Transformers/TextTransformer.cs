using PLang.Errors;
using PLang.Errors.Runtime;
using PLang.Interfaces;
using PLang.Services.OutputStream.Messages;
using PLang.Utils;
using System.IO.Pipelines;
using System.Text;

namespace PLang.Services.OutputStream.Transformers;

/// <summary>
/// Text transformer that outputs plain text.
/// For default channel: just outputs content.
/// For other channels: includes channel, level, and other metadata.
/// </summary>
public class TextTransformer : ITransformer
{
	protected readonly Encoding encoding;

	public TextTransformer(Encoding encoding)
	{
		this.encoding = encoding;
	}

	public Encoding Encoding => encoding;
	public virtual string ContentType => "text/plain";

	public async Task<(long, IError?)> Transform(PLangContext context, PipeWriter writer, OutMessage obj, CancellationToken ct = default)
	{
		if (obj == null) return (0, null);

		SemaphoreSlim? gate = null;
		try
		{
			var output = FormatOutput(obj);
			if (string.IsNullOrEmpty(output)) return (0, null);

			var needed = encoding.GetByteCount(output);
			gate = await TransformerHelper.GetGate(context.SharedItems, ct);
			var span = writer.GetSpan(needed);

			var length = encoding.GetBytes(output.AsSpan(), span);
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

	/// <summary>
	/// Formats output based on channel.
	/// Default channel: just content.
	/// Other channels: [channel] [level] content
	/// </summary>
	protected virtual string? FormatOutput(OutMessage obj)
	{
		var content = GetContent(obj);
		if (content == null) return null;

		var isDefaultChannel = string.IsNullOrEmpty(obj.Channel) ||
							   obj.Channel.Equals("default", StringComparison.OrdinalIgnoreCase);

		if (isDefaultChannel || this is HtmlTransformer)
		{
			// Default channel: just content with optional newline
			return AppendNewlineIfNeeded(content, obj);
		}

		// Non-default channel: include metadata
		// Format: [channel] [level] [status] content
		var prefix = BuildPrefix(obj);
		return AppendNewlineIfNeeded($"{prefix}{content}", obj);
	}

	protected virtual string? GetContent(OutMessage obj)
	{
		return obj switch
		{
			TextMessage tm => tm.Content,
			RenderMessage rm => rm.Content,
			ErrorMessage em => em.Content,
			ExecuteMessage ex => $"[Execute] {ex.Function}({StringHelper.ConvertToString(ex.Data)})",
			AskMessage am => $"[Ask] {am.Content}",
			StreamMessage sm => sm.Text ?? (sm.HasBinary ? $"[Binary {sm.Bytes?.Length ?? 0} bytes]" : null),
			_ => StringHelper.ConvertToString(obj)
		};
	}

	protected virtual string BuildPrefix(OutMessage obj)
	{
		var parts = new List<string>();

		// Channel
		if (!string.IsNullOrEmpty(obj.Channel))
		{
			parts.Add($"[{obj.Channel}]");
		}

		// Level (if not info)
		if (!string.IsNullOrEmpty(obj.Level) && !obj.Level.Equals("info", StringComparison.OrdinalIgnoreCase))
		{
			parts.Add($"[{obj.Level.ToUpperInvariant()}]");
		}

		// Status code (if not 200)
		if (obj.StatusCode != 200)
		{
			parts.Add($"[{obj.StatusCode}]");
		}

		// Actor (if not user)
		if (!string.IsNullOrEmpty(obj.Actor) && !obj.Actor.Equals("user", StringComparison.OrdinalIgnoreCase))
		{
			parts.Add($"[{obj.Actor}]");
		}

		if (parts.Count == 0) return "";
		return string.Join(" ", parts) + " ";
	}

	protected virtual string AppendNewlineIfNeeded(string content, OutMessage obj)
	{
		var skipNewline = obj is TextMessage tm && tm.SkipNewline;
		return skipNewline ? content : content + Environment.NewLine;
	}
}
