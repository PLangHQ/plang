using System;
using System.Collections.Generic;

namespace PLang.Tests.App.Types.PathTests.Http;

/// <summary>
/// SKELETON — in-process HTTP test server for <c>HttpPath</c> tests (stage 5) and the
/// <c>HttpPathFixture</c> contract fixture (stage 7). The coder implements this in stage 5.
///
/// Recommended implementation: <c>System.Net.HttpListener</c> — in-box, no package, no
/// ASP.NET framework reference needed in <c>PLang.Tests.csproj</c>. (The architect's plan
/// said "in-process Kestrel"; <c>HttpListener</c> achieves the same with zero new
/// dependency. If the coder prefers Kestrel, add
/// <c>&lt;FrameworkReference Include="Microsoft.AspNetCore.App"/&gt;</c> — but HttpListener
/// is the lighter choice and is what this skeleton is shaped for.)
///
/// Required behaviour:
///  - Binds a free loopback port on construction; <see cref="BaseAddress"/> exposes it.
///  - A verb-aware in-memory store keyed at <c>/contract/{guid}</c>:
///      GET    → 200 + stored bytes, or 404 if absent.
///      POST   → stores the request body, 200 (or 201).
///      PUT    → stores the request body, 200.
///      DELETE → removes the entry, 204.
///      HEAD   → 200 with Content-Length + Last-Modified, or 404.
///  - Error-injection routes for the negative tests:
///      <see cref="MapStatus"/> — force a fixed status on a path (404, 500, ...).
///      <see cref="MapGetOnly"/> — a path that 405s on POST/PUT/DELETE (the
///        "let the server respond" 405 case).
///      <see cref="MapRequiresIdentity"/> — 401 unless PLang signing-identity headers are
///        present on the request.
///  - Captures every request into <see cref="Requests"/> so tests can assert on identity
///    headers, call counts, and per-call independence.
///  - <see cref="IDisposable"/> — stops the listener and frees the port.
/// </summary>
public sealed class HttpTestServer : IDisposable
{
    /// <summary>Loopback base address, e.g. <c>http://127.0.0.1:{port}/</c>.</summary>
    public Uri BaseAddress => throw new NotImplementedException();

    /// <summary>Every request the server received, in order — for header / count asserts.</summary>
    public IReadOnlyList<RequestRecord> Requests => throw new NotImplementedException();

    /// <summary>Mints a fresh, unused <c>/contract/{guid}</c> absolute URL.</summary>
    public string NewResourceUrl() => throw new NotImplementedException();

    /// <summary>Forces a fixed HTTP status on the given absolute-or-relative path.</summary>
    public void MapStatus(string path, int status) => throw new NotImplementedException();

    /// <summary>Registers a path that answers GET but 405s on any mutating verb.</summary>
    public string MapGetOnly() => throw new NotImplementedException();

    /// <summary>Registers a path that 401s unless PLang signing-identity headers are present.</summary>
    public string MapRequiresIdentity() => throw new NotImplementedException();

    public void Dispose() => throw new NotImplementedException();

    /// <summary>One captured request — verb, path, headers, body length.</summary>
    public sealed record RequestRecord(
        string Method,
        string Path,
        IReadOnlyDictionary<string, string> Headers,
        long BodyLength);
}
