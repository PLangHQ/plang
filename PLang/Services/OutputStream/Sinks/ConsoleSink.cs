using PLang.Errors;
using PLang.Services.OutputStream.Messages;
using System.Text;
using System.Text.Json;

namespace PLang.Services.OutputStream.Sinks;

public sealed class ConsoleSink : IOutputSink
{
	readonly Stream _stdout;
	readonly Stream _stderr;

	public bool IsStateful => true;

	public string Id { get; } = Guid.NewGuid().ToString();

	public ConsoleSink()
	{
		Console.OutputEncoding = Encoding.UTF8;
		Console.InputEncoding = Encoding.UTF8;
		_stdout = Console.OpenStandardOutput();
		_stderr = Console.OpenStandardError();
	}

	public Task<IError?> SendAsync(OutMessage m, CancellationToken ct = default) =>
		m switch
		{
			TextMessage t => WriteText(t),
			RenderMessage r => WriteRender(r),
			ExecuteMessage e => WriteExecute(e),
			AskMessage a => WriteAskNotice(a),
			_ => Task.FromResult<IError?>(null)
		};

	public async Task<(object? result, IError? error)> AskAsync(AskMessage a, CancellationToken ct = default)
	{
		SetColor(a.StatusCode, a.Level);
		WritePrefix(a.Target, a.Actions);

		Console.WriteLine($"[Ask] {a.Content}");
		Console.ResetColor();
		Console.Write("> ");
		var raw = Console.ReadLine() ?? string.Empty;
		return (raw, null);
	}

	Task<IError?> WriteText(TextMessage t)
	{
		SetColor(t.StatusCode, t.Level);
		WritePrefix(t.Target, t.Actions);
		if (!IsPrimitiveOrString(t.Content))
			Console.WriteLine(JsonSerializer.Serialize(t.Content));
		else
			Console.WriteLine(t.Content);
		Console.ResetColor();
		return Task.FromResult<IError?>(null);
	}

	Task<IError?> WriteRender(RenderMessage r)
	{
		SetColor(r.StatusCode, r.Level);
		WritePrefix(r.Target, r.Actions);
		Console.WriteLine("[Render]");
		Console.WriteLine(Trunc(r.Content, 2000));
		Console.ResetColor();
		return Task.FromResult<IError?>(null);
	}

	Task<IError?> WriteExecute(ExecuteMessage e)
	{
		SetColor(e.StatusCode, e.Level);
		WritePrefix(e.Target, Array.Empty<string>());
		
		var data = JsonSerializer.Serialize(e.Data);
		Console.WriteLine($"[Execute] {e.Function}({data})");
		Console.ResetColor();
		return Task.FromResult<IError?>(null);
	}


	Task<IError?> WriteAskNotice(AskMessage a)
	{
		SetColor(a.StatusCode, a.Level);
		WritePrefix(a.Target, a.Actions);
		Console.WriteLine($"[Ask->Template] {a.Content}");
		Console.ResetColor();
		return Task.FromResult<IError?>(null);
	}

	static void WritePrefix(string? target, IReadOnlyList<string> actions)
	{
		var tgt = string.IsNullOrWhiteSpace(target) ? "-" : target;
		var act = (actions?.Count ?? 0) > 0 ? string.Join(" | ", actions!) : "-";
		Console.Write($"[{tgt}] [{act}] ");
	}

	static void SetColor(int status, string level, bool isError = false)
	{
		if (isError || level.Equals("error", StringComparison.OrdinalIgnoreCase) || status >= 500)
		{
			Console.ForegroundColor = ConsoleColor.Red;
			Console.BackgroundColor = ConsoleColor.Yellow;
			return;
		}
		if (status >= 400 || level.Equals("warning", StringComparison.OrdinalIgnoreCase))
		{
			Console.ForegroundColor = ConsoleColor.Yellow;
			Console.BackgroundColor = ConsoleColor.DarkRed;
			return;
		}
		if (status >= 300) { Console.ForegroundColor = ConsoleColor.Magenta; return; }
		if (status >= 200) { Console.ResetColor(); return; }
		if (status >= 100) { Console.ForegroundColor = ConsoleColor.Cyan; return; }
		Console.ResetColor();
	}

	static bool IsPrimitiveOrString(object? o)
	{
		if (o is null) return true;
		var t = o.GetType();
		return t.IsPrimitive || t.IsEnum || t == typeof(string) || t == typeof(decimal);
	}

	static string Trunc(string s, int max) => s.Length <= max ? s : s[..max] + "…";


}
