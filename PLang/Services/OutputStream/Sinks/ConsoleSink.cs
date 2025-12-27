using PLang.Errors;
using PLang.Services.OutputStream.Messages;
using PLang.Utils;
using System.Text;
using System.Text.Json;

namespace PLang.Services.OutputStream.Sinks;

/// <summary>
/// Console sink for outputting messages to stdout/stderr.
/// 
/// For default channel: outputs just the content.
/// For other channels: includes [channel] [level] prefix.
/// 
/// Colors are applied based on level and status code.
/// </summary>
public sealed class ConsoleSink : IOutputSink
{
	public bool IsStateful => true;
	public string Id { get; } = Guid.NewGuid().ToString();

	public ConsoleSink()
	{
		Console.OutputEncoding = Encoding.UTF8;
		Console.InputEncoding = Encoding.UTF8;
	}

	public Task<IError?> SendAsync(OutMessage message, CancellationToken ct = default)
	{
		var content = FormatOutput(message);
		if (string.IsNullOrEmpty(content))
			return Task.FromResult<IError?>(null);

		SetColor(message.StatusCode, message.Level);

		if (message is TextMessage tm && tm.SkipNewline)
			Console.Write(content);
		else
			Console.WriteLine(content);

		Console.ResetColor();
		return Task.FromResult<IError?>(null);
	}

	public Task<(object? result, IError? error)> AskAsync(AskMessage message, CancellationToken ct = default)
	{
		SetColor(message.StatusCode, message.Level);

		var content = FormatOutput(message);
		Console.WriteLine(content);

		Console.ResetColor();
		Console.Write("> ");

		var input = Console.ReadLine() ?? string.Empty;
		return Task.FromResult<(object?, IError?)>((input, null));
	}

	/// <summary>
	/// Formats output based on channel.
	/// Default channel: just content.
	/// Other channels: [channel] [level] content
	/// </summary>
	private string? FormatOutput(OutMessage message)
	{
		var content = GetContent(message);
		if (content == null) return null;

		var isDefaultChannel = string.IsNullOrEmpty(message.Channel) ||
							   message.Channel.Equals("default", StringComparison.OrdinalIgnoreCase);

		if (isDefaultChannel)
		{
			return content;
		}

		// Non-default channel: include metadata prefix
		var prefix = BuildPrefix(message);
		return $"{prefix}{content}";
	}

	private string? GetContent(OutMessage message)
	{
		return message switch
		{
			TextMessage tm => FormatValue(tm.Content),
			RenderMessage rm => $"[Render] {Truncate(rm.Content, 2000)}",
			ErrorMessage em => em.Content,
			ExecuteMessage ex => $"[Execute] {ex.Function}({FormatValue(ex.Data)})",
			AskMessage am => am.Content,
			StreamMessage sm => sm.Text ?? (sm.HasBinary ? $"[Binary {sm.Bytes?.Length ?? 0} bytes]" : null),
			_ => StringHelper.ConvertToString(message)
		};
	}

	private string BuildPrefix(OutMessage message)
	{
		var parts = new List<string>();

		// Channel
		if (!string.IsNullOrEmpty(message.Channel))
		{
			parts.Add($"[{message.Channel}]");
		}

		// Level (if not info)
		if (!string.IsNullOrEmpty(message.Level) && !message.Level.Equals("info", StringComparison.OrdinalIgnoreCase))
		{
			parts.Add($"[{message.Level.ToUpperInvariant()}]");
		}

		// Status code (if not 200)
		if (message.StatusCode != 200)
		{
			parts.Add($"[{message.StatusCode}]");
		}

		// Actor (if not user)
		if (!string.IsNullOrEmpty(message.Actor) && !message.Actor.Equals("user", StringComparison.OrdinalIgnoreCase))
		{
			parts.Add($"[{message.Actor}]");
		}

		if (parts.Count == 0) return "";
		return string.Join(" ", parts) + " ";
	}

	private static string FormatValue(object? value)
	{
		if (value == null) return "";
		if (IsPrimitiveOrString(value)) return value.ToString() ?? "";
		return JsonSerializer.Serialize(value);
	}

	private static void SetColor(int statusCode, string level)
	{
		// Error level or 5xx status
		if (level.Equals("error", StringComparison.OrdinalIgnoreCase) ||
			level.Equals("critical", StringComparison.OrdinalIgnoreCase) ||
			statusCode >= 500)
		{
			Console.ForegroundColor = ConsoleColor.Red;
			return;
		}

		// Warning level or 4xx status
		if (level.Equals("warning", StringComparison.OrdinalIgnoreCase) || statusCode >= 400)
		{
			Console.ForegroundColor = ConsoleColor.Yellow;
			return;
		}

		// Debug/trace level
		if (level.Equals("debug", StringComparison.OrdinalIgnoreCase) ||
			level.Equals("trace", StringComparison.OrdinalIgnoreCase))
		{
			Console.ForegroundColor = ConsoleColor.DarkGray;
			return;
		}

		// 3xx status (redirects)
		if (statusCode >= 300)
		{
			Console.ForegroundColor = ConsoleColor.Magenta;
			return;
		}

		// 1xx status (informational)
		if (statusCode < 200)
		{
			Console.ForegroundColor = ConsoleColor.Cyan;
			return;
		}

		// Default (2xx, info level)
		Console.ResetColor();
	}

	private static bool IsPrimitiveOrString(object? value)
	{
		if (value == null) return true;
		var type = value.GetType();
		return type.IsPrimitive || type.IsEnum || type == typeof(string) || type == typeof(decimal);
	}

	private static string Truncate(string value, int maxLength)
	{
		return value.Length <= maxLength ? value : value[..maxLength] + "…";
	}
}