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
	public bool IsFlushed
	{
		get
		{
			try
			{
				return context.HttpContext?.Response.HasStarted ?? true;
			} catch
			{
				return true;
			}
		}
	}
	public HttpSink(PLangContext context, WebserverProperties props, ConcurrentDictionary<string, LiveConnection> live)
	{
		this.context = context;
		_props = props;
		_live = live;
		_path = context.HttpContext!.Request.Path.Value ?? "/";
		_transformer = ChooseTransformer(props, context.HttpContext!, context);
	}

	public bool IsComplete { get; set; }
	public ConcurrentDictionary<string, LiveConnection> LiveConnections { get { return _live; } }
	public async Task<IError?> SendAsync(OutMessage m, CancellationToken ct = default)
	{
		var (response, wasFlushed, error) = GetResponse();
		if (error != null) return error;
		if (response is null || !response.Body.CanWrite || IsComplete) return null;

		// Get the effective transformer - may have changed due to ConfigureOutput
		var transformer = GetEffectiveTransformer(m);

		if (!wasFlushed && !response.HasStarted && response.StatusCode == 200)
		{
			response.StatusCode = m.StatusCode == 0 ? 200 : m.StatusCode;
			response.ContentType = $"{transformer.ContentType}; charset={transformer.Encoding.WebName}";

			if (transformer is PlangTransformer)
			{
				response.Headers.CacheControl = "no-cache, no-store, must-revalidate";
				response.Headers.Pragma = "no-cache";
				response.Headers.Expires = "0";
				response.Headers["X-Accel-Buffering"] = "no";
			}
		}

		m = BuildResponseProperties(m);

		var writer = response.BodyWriter;

		(var length, error) = await transformer.Transform(context, writer, m);
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

	/// <summary>
	/// Gets the effective transformer for a message, checking for explicitly configured content type overrides.
	/// Only overrides the Accept-header-based transformer if content type was explicitly set via ConfigureOutput.
	/// </summary>
	ITransformer GetEffectiveTransformer(OutMessage m)
	{
		// Only check for EXPLICIT content type configuration (via ConfigureOutput command)
		// Don't override Accept-header-based transformer with actor defaults
		var explicitContentType = context.GetExplicitContentType(m.Actor, m.Channel);

		if (!string.IsNullOrEmpty(explicitContentType))
		{
			var configuredEncoding = context.GetEffectiveEncoding(m.Actor, m.Channel);
			var enc = configuredEncoding ?? Encoding.GetEncoding(_props.DefaultResponseProperties!.ResponseEncoding);
			return CreateTransformerForContentType(explicitContentType, enc);
		}

		// Fall back to Accept-header-based transformer (chosen in constructor)
		return _transformer;
	}

	static ITransformer CreateTransformerForContentType(string contentType, Encoding enc)
	{
		if (contentType.StartsWith("application/plang", StringComparison.OrdinalIgnoreCase) ||
			contentType.StartsWith("application/x-ndjson", StringComparison.OrdinalIgnoreCase))
			return new PlangTransformer(enc);
		if (contentType.StartsWith("application/json", StringComparison.OrdinalIgnoreCase))
			return new JsonTransformer(enc);
		if (contentType.StartsWith("text/html", StringComparison.OrdinalIgnoreCase))
			return new HtmlTransformer(enc);
		return new TextTransformer(enc);
	}

	/// <summary>
	/// Chooses the initial transformer based on the Accept header.
	/// This sets the default, which can be overridden by explicit ConfigureOutput calls.
	/// </summary>
	static ITransformer ChooseTransformer(WebserverProperties props, HttpContext ctx, PLangContext? plangContext = null)
	{
		var acceptHeader = ctx.Request.Headers.Accept.FirstOrDefault() ?? "";
		var defaultEnc = Encoding.GetEncoding(props.DefaultResponseProperties!.ResponseEncoding);

		// Only use plang/ndjson format if client explicitly requests it
		if (acceptHeader.Contains("application/plang", StringComparison.OrdinalIgnoreCase) ||
			acceptHeader.Contains("application/x-ndjson", StringComparison.OrdinalIgnoreCase))
		{
			return new PlangTransformer(defaultEnc);
		}

		// For other requests, choose based on Accept header preference
		if (acceptHeader.Contains("application/json", StringComparison.OrdinalIgnoreCase))
		{
			return new JsonTransformer(defaultEnc);
		}

		// Default to HTML for browsers (text/html, */* or empty Accept)
		// This covers standard browser requests
		return new HtmlTransformer(defaultEnc);
	}

	OutMessage BuildResponseProperties(OutMessage m)
	{
		var ui = context.UiOutputProperties;
		if (ui == null) return m;

		if (!string.IsNullOrEmpty(ui.Target)) { 
			m = m with { Target = ui.Target };
		}

		if (m is ErrorMessage)
		{
			m = m with { Target = ui.ErrorTarget };
		}

		if (ui.Actions != null && ui.Actions.Count > 0)
		{
			m = m with { Actions = ui.Actions.ToList() };
		}

		return m;
	}

	public (HttpResponse? response, bool wasFlushed, IError? error) GetResponse()
	{
		try
		{
			UpdateLiveConnection(false);
			if (!context.IsAsync && context.HttpContext?.Response.Body.CanWrite == true) return (context.HttpContext!.Response, false, null);
		}
		catch { /* ignore */ }

		try
		{
			var (response, was) = UpdateLiveConnection(true);			
			return (response, was, null);
		}
		catch (Exception ex)
		{
			Console.WriteLine("Live connection unavailable: " + ex);
			_live.TryRemove(context.Identity, out _);
			return (null, true, null);
		}
	}

	private (HttpResponse?, bool WasFlushed) UpdateLiveConnection(bool isFlushing)
	{
		if (_live is null || string.IsNullOrEmpty(context.Identity)) return (null, false);
		if (!_live.TryGetValue(context.Identity, out var live) || live == null) return (null, false);

		bool wasFlushed = live.IsFlushed;
		if (isFlushing)
		{
			live.IsFlushed = isFlushing;
		}
		live.Updated = DateTime.Now;

		_live.AddOrReplace(context.Identity, live);

		return (live.Response, wasFlushed);
	}
}
