using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace PLang.Tests.App.Types.PathTests.Http;

/// <summary>
/// In-process HTTP test server for <c>HttpPath</c> tests (stage 5) and the
/// <c>HttpPathFixture</c> contract fixture (stage 7). Built on
/// <see cref="HttpListener"/> — in-box, no package, no ASP.NET reference.
///
/// Verb-aware in-memory store keyed at <c>/contract/{guid}</c>:
///   GET    → 200 + stored bytes, or 404 if absent.
///   POST   → stores the request body, 200.
///   PUT    → stores the request body, 200.
///   DELETE → removes the entry, 204.
///   HEAD   → 200 with Content-Length + Last-Modified, or 404.
///
/// Error-injection routes: <see cref="MapStatus"/>, <see cref="MapGetOnly"/>,
/// <see cref="MapRequiresIdentity"/>. Every request is captured into
/// <see cref="Requests"/>.
/// </summary>
public sealed class HttpTestServer : IDisposable
{
    private readonly HttpListener _listener;
    private readonly Uri _baseAddress;
    private readonly ConcurrentDictionary<string, byte[]> _store = new();
    private readonly ConcurrentDictionary<string, int> _forcedStatus = new();
    private readonly ConcurrentBag<string> _getOnlyPaths = new();
    private readonly ConcurrentBag<string> _identityPaths = new();
    private readonly List<RequestRecord> _requests = new();
    private readonly object _requestsLock = new();
    private readonly CancellationTokenSource _cts = new();
    private volatile bool _disposed;

    public HttpTestServer()
    {
        // Bind a free loopback port: probe with a TcpListener, then hand the
        // port to HttpListener.
        int port = FreePort();
        _baseAddress = new Uri($"http://127.0.0.1:{port}/");
        _listener = new HttpListener();
        _listener.Prefixes.Add(_baseAddress.ToString());
        _listener.Start();
        _ = Task.Run(AcceptLoop);
    }

    public Uri BaseAddress => _baseAddress;

    public IReadOnlyList<RequestRecord> Requests
    {
        get { lock (_requestsLock) return _requests.ToArray(); }
    }

    public string NewResourceUrl() => $"{_baseAddress}contract/{Guid.NewGuid():N}";

    public void MapStatus(string path, int status) => _forcedStatus[Normalize(path)] = status;

    public string MapGetOnly()
    {
        var path = $"/getonly/{Guid.NewGuid():N}";
        _getOnlyPaths.Add(path);
        _store[path] = Encoding.UTF8.GetBytes("get-only resource");
        return _baseAddress.GetLeftPart(UriPartial.Authority) + path;
    }

    public string MapRequiresIdentity()
    {
        var path = $"/secure/{Guid.NewGuid():N}";
        _identityPaths.Add(path);
        _store[path] = Encoding.UTF8.GetBytes("secured resource");
        return _baseAddress.GetLeftPart(UriPartial.Authority) + path;
    }

    private static int FreePort()
    {
        var probe = new System.Net.Sockets.TcpListener(IPAddress.Loopback, 0);
        probe.Start();
        int port = ((IPEndPoint)probe.LocalEndpoint).Port;
        probe.Stop();
        return port;
    }

    private static string Normalize(string pathOrUrl)
    {
        if (Uri.TryCreate(pathOrUrl, UriKind.Absolute, out var u)) return u.AbsolutePath;
        return pathOrUrl.StartsWith('/') ? pathOrUrl : "/" + pathOrUrl;
    }

    private async Task AcceptLoop()
    {
        while (!_disposed)
        {
            HttpListenerContext ctx;
            try { ctx = await _listener.GetContextAsync(); }
            catch { return; }   // listener stopped
            _ = Task.Run(() => Handle(ctx));
        }
    }

    private void Handle(HttpListenerContext ctx)
    {
        var req = ctx.Request;
        var resp = ctx.Response;
        var path = req.Url!.AbsolutePath;

        // Capture the request.
        var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (string key in req.Headers.AllKeys!)
            if (key != null) headers[key] = req.Headers[key] ?? "";
        long bodyLen = 0;
        byte[] body = Array.Empty<byte>();
        if (req.HasEntityBody)
        {
            using var ms = new System.IO.MemoryStream();
            req.InputStream.CopyTo(ms);
            body = ms.ToArray();
            bodyLen = body.Length;
        }
        lock (_requestsLock)
            _requests.Add(new RequestRecord(req.HttpMethod, path, headers, bodyLen));

        try
        {
            // Forced-status injection.
            if (_forcedStatus.TryGetValue(path, out var forced))
            {
                resp.StatusCode = forced;
                resp.Close();
                return;
            }

            // Identity-required routes.
            if (_identityPaths.Contains(path) && string.IsNullOrEmpty(req.Headers["X-Signature"]))
            {
                resp.StatusCode = 401;
                resp.Close();
                return;
            }

            // GET-only routes 405 on mutating verbs.
            if (_getOnlyPaths.Contains(path)
                && req.HttpMethod is not ("GET" or "HEAD"))
            {
                resp.StatusCode = 405;
                resp.Close();
                return;
            }

            switch (req.HttpMethod)
            {
                case "GET":
                    if (_store.TryGetValue(path, out var getBytes))
                    {
                        resp.StatusCode = 200;
                        resp.ContentLength64 = getBytes.Length;
                        resp.OutputStream.Write(getBytes, 0, getBytes.Length);
                    }
                    else resp.StatusCode = 404;
                    break;

                case "HEAD":
                    if (_store.TryGetValue(path, out var headBytes))
                    {
                        resp.StatusCode = 200;
                        resp.ContentLength64 = headBytes.Length;
                        resp.Headers["Last-Modified"] = DateTime.UtcNow.ToString("R");
                    }
                    else resp.StatusCode = 404;
                    break;

                case "POST":
                case "PUT":
                    _store[path] = body;
                    resp.StatusCode = 200;
                    break;

                case "DELETE":
                    _store.TryRemove(path, out _);
                    resp.StatusCode = 204;
                    break;

                default:
                    resp.StatusCode = 405;
                    break;
            }
            resp.Close();
        }
        catch
        {
            try { resp.Abort(); } catch { /* already closed */ }
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _cts.Cancel();
        try { _listener.Stop(); } catch { }
        try { _listener.Close(); } catch { }
        _cts.Dispose();
    }

    public sealed record RequestRecord(
        string Method,
        string Path,
        IReadOnlyDictionary<string, string> Headers,
        long BodyLength);
}
