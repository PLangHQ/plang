using PLang.Errors;
using PLang.Services.OutputStream.Messages;
using PLang.Services.OutputStream.Transformers;
using System.Collections.Concurrent;
using System.Text;
using static PLang.Modules.WebserverModule.Program;
using static PLang.Runtime.Engine;

namespace PLang.Services.OutputStream.Sinks;





public sealed class HttpSink : IOutputSink
{
	readonly HttpContext _ctx;
	readonly WebserverProperties _props;
	readonly ConcurrentDictionary<string, LiveConnection> _live;
	readonly ITransformer _transformer;

	string? _identity;
	string _path;

	public string Id { get; } = Guid.NewGuid().ToString();
	public bool IsStateful => true;
	public bool IsFlushed => _ctx.Response.HasStarted;
	public Dictionary<string, object?> ResponseProperties { get; set; } = new();
	public HttpSink(HttpContext ctx, WebserverProperties props, ConcurrentDictionary<string, LiveConnection> live)
	{
		_ctx = ctx;
		_props = props;
		_live = live;
		_path = ctx.Request.Path.Value ?? "/";
		_transformer = ChooseTransformer(props, ctx);
	}

	public void SetIdentity(string identity) => _identity = identity;
	public bool IsComplete { get; set; }
	public ConcurrentDictionary<string, LiveConnection> LiveConnections { get { return _live; } }
	public async Task<IError?> SendAsync(OutMessage m, CancellationToken ct = default)
	{
		var (response, wasFlushed, error) = GetResponse();
		if (error != null) return error;
		if (response is null || !response.Body.CanWrite) return null;

		if (!wasFlushed && !response.HasStarted && response.StatusCode == 200)
		{
			response.StatusCode = m.StatusCode == 0 ? 200 : m.StatusCode;
			response.ContentType = $"{_transformer.ContentType}; charset={_transformer.Encoding.WebName}";

			if (_transformer is PlangTransformer)
			{
				response.Headers.CacheControl = "no-cache";
				response.Headers.Pragma = "no-cache";
				response.Headers.Expires = "0";
				response.Headers["X-Accel-Buffering"] = "no";
			}
		}

		var props = BuildResponseProperties(m);

		var writer = response.BodyWriter;

		(var length, error) = await _transformer.Transform(response.HttpContext, writer, m);
		if (error != null) return error;

		await writer.FlushAsync(ct);

		return null;
	}

	public async Task<(object? result, IError? error)> AskAsync(AskMessage m, CancellationToken ct = default)
	{
		// For HTTP you said you have the round-trip solved. We just emit the ask envelope.
		var err = await SendAsync(m, ct);
		return (null, err);
	}

	// ---------- helpers ----------

	static ITransformer ChooseTransformer(WebserverProperties props, HttpContext ctx)
	{
		var accept = ctx.Request.Headers.Accept.FirstOrDefault()
					 ?? props.DefaultResponseProperties!.ContentType;
		var enc = Encoding.GetEncoding(props.DefaultResponseProperties!.ResponseEncoding);

		if (accept.StartsWith("application/plang", StringComparison.OrdinalIgnoreCase)) return new PlangTransformer(enc);
		if (accept.StartsWith("application/json", StringComparison.OrdinalIgnoreCase)) return new JsonTransformer(enc);
		if (accept.StartsWith("text/html", StringComparison.OrdinalIgnoreCase)) return new HtmlTransformer(enc);
		return new TextTransformer(enc);
	}

	object BuildPayload(OutMessage m) =>
		m switch
		{
			TextMessage t => new { kind = "text", level = m.Level, status = m.StatusCode, target = m.Target, actions = m.Actions, meta = m.Meta, content = t.Content },
			RenderMessage r => new { kind = "render", level = m.Level, status = m.StatusCode, target = m.Target, actions = m.Actions, meta = m.Meta, content = r.Content },
			ExecuteMessage e => new { kind = "execute", level = m.Level, status = m.StatusCode, target = m.Target, actions = m.Actions, meta = m.Meta, function = e.Function, data = e.Data },
			AskMessage a => new { kind = "ask", level = m.Level, status = m.StatusCode, target = m.Target, actions = m.Actions, meta = m.Meta, content = a.Content },
			_ => new { kind = "unknown" }
		};

	Dictionary<string, object?> BuildResponseProperties(OutMessage m)
	{
		var dict = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
		{
			["path"] = _path
		};

		if (!string.IsNullOrWhiteSpace(m.Target)) dict["cssSelector"] = m.Target; // legacy hint
		if (m.Actions?.Count > 0) dict["actions"] = m.Actions;
		if (m.Meta is not null)
			foreach (var kv in m.Meta) if (!dict.ContainsKey(kv.Key)) dict[kv.Key] = kv.Value;

		// Back-compat with any existing “data-plang-*” props that your transformers understand:
		if (!dict.ContainsKey("data-plang-cssSelector") && dict.TryGetValue("cssSelector", out var sel)) dict["data-plang-cssSelector"] = sel;
		if (!dict.ContainsKey("data-plang-action") && dict.TryGetValue("actions", out var act)) dict["data-plang-action"] = act;

		return dict;
	}

	public (HttpResponse? response, bool wasFlushed, IError? error) GetResponse()
	{
		try
		{
			if (_ctx.Response.Body.CanWrite) return (_ctx.Response, false, null);
		}
		catch { /* ignore */ }

		try
		{
			if (_live is null || string.IsNullOrEmpty(_identity)) return (null, false, null);
			if (!_live.TryGetValue(_identity!, out var live)) return (null, false, null);
			var was = live.IsFlushed;
			live.IsFlushed = true;
			return (live.Response, was, null);
		}
		catch (Exception ex)
		{
			Console.WriteLine("Live connection unavailable: " + ex);
			_live.TryRemove(_identity!, out _);
			return (null, true, null);
		}
	}
}
