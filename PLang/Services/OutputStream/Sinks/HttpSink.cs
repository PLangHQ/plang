using LightInject;
using NBitcoin;
using Newtonsoft.Json;
using NSec.Cryptography;
using PLang.Building.Model;
using PLang.Errors;
using PLang.Interfaces;
using PLang.Services.OutputStream.Messages;
using PLang.Services.OutputStream.Transformers;
using PLang.Utils;
using Sprache;
using System.Collections.Concurrent;
using System.Text;
using static PLang.Modules.WebserverModule.Program;
using static PLang.Runtime.Engine;
using static PLang.Utils.StepHelper;

namespace PLang.Services.OutputStream.Sinks;





public sealed class HttpSink : IOutputSink
{
	readonly PLangContext context;
	readonly WebserverProperties _props;
	readonly ConcurrentDictionary<string, LiveConnection> _live;
	readonly ITransformer _transformer;

	string _path;

	public string Id { get; } = Guid.NewGuid().ToString();
	public bool IsStateful => false;
	public bool IsFlushed => context.HttpContext?.Response.HasStarted ?? true;
	public Dictionary<string, object?> ResponseProperties { get; set; } = new();
	public HttpSink(PLangContext context, WebserverProperties props, ConcurrentDictionary<string, LiveConnection> live)
	{
		this.context = context;
		_props = props;
		_live = live;
		_path = context.HttpContext!.Request.Path.Value ?? "/";
		_transformer = ChooseTransformer(props, context.HttpContext!);
	}

	public bool IsComplete { get; set; }
	public ConcurrentDictionary<string, LiveConnection> LiveConnections { get { return _live; } }
	public async Task<IError?> SendAsync(OutMessage m, CancellationToken ct = default)
	{
		var (response, wasFlushed, error) = GetResponse();
		if (error != null) return error;
		if (response is null || !response.Body.CanWrite || IsComplete) return null;

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

		m = BuildResponseProperties(m);

		var writer = response.BodyWriter;

		(var length, error) = await _transformer.Transform(context, writer, m);
		if (error != null) return error;
		try
		{
			if (writer.UnflushedBytes > 0)
			{
				await writer.FlushAsync(ct);
			}
		} catch (Exception ex)
		{
			Console.WriteLine(ex.ToString());
		}
		

		return null;
	}

	public async Task<(object? result, IError? error)> AskAsync(AskMessage m, CancellationToken ct = default)
	{
		var err = await SendAsync(m, ct);
		return (null, err);
	}

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

	OutMessage BuildResponseProperties(OutMessage m)
	{
	

		if (ResponseProperties.TryGetValue("p-target", out object? target) && !string.IsNullOrWhiteSpace(target?.ToString()) && string.IsNullOrEmpty(m.Target))
		{
			m = m with { Target = target.ToString()! };
		}


		if (ResponseProperties.TryGetValue("p-actions", out object? actions) && !string.IsNullOrWhiteSpace(actions?.ToString()) && (m.Actions == null || m.Actions.Count == 0))
		{
			var actionArray = actions.ToString()?.Split(' ');
			if (actionArray != null)
			{
				m = m with { Actions = actionArray.ToList() };
			}
		}



		return m;
	}

	public (HttpResponse? response, bool wasFlushed, IError? error) GetResponse()
	{
		try
		{
			if (context.HttpContext?.Response.Body.CanWrite == true) return (context.HttpContext!.Response, false, null);
		}
		catch { /* ignore */ }

		try
		{
			if (_live is null || string.IsNullOrEmpty(context.Identity)) return (null, false, null);
			if (!_live.TryGetValue(context.Identity, out var live)) return (null, false, null);
			var was = live.IsFlushed;
			live.IsFlushed = true;
			return (live.Response, was, null);
		}
		catch (Exception ex)
		{
			Console.WriteLine("Live connection unavailable: " + ex);
			_live.TryRemove(context.Identity, out _);
			return (null, true, null);
		}
	}
}
