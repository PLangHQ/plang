using System;
using System.Threading.Tasks;
using PLang.Tests.App.Types.PathTests.Http;
// See IPathSchemeFixture.cs — current-type alias, repointed by stage 1's rename sweep.
using Path = global::app.filesystem.path;

namespace PLang.Tests.App.Types.PathTests.Contract;

/// <summary>
/// SKELETON — the <see cref="IPathSchemeFixture"/> for the <c>http</c> scheme. The coder
/// implements this in stage 7.
///
/// Required behaviour:
///  - Constructor spins up an <see cref="HttpTestServer"/> (in-process loopback) and an App
///    so minted HttpPaths have a Context/Actor for Authorize.
///  - <see cref="CreateFresh"/> mints an <c>HttpPath</c> at a fresh
///    <c>HttpTestServer.NewResourceUrl()</c>, Context-wired. The server's verb-aware
///    in-memory store backs Write/Read/Delete/Exists/Stat round-trips.
///  - <see cref="Cleanup"/> removes the server-side entry — idempotent.
///  - <see cref="CanPerform"/> returns false for <see cref="VerbName.List"/> (the test
///    server has no directory-listing concept) and true for the rest. This SCOPES the
///    <c>List</c> contract test out for HTTP — it does not skip an assertion on a verb the
///    server merely refuses.
///  - Implement <c>IDisposable</c> — dispose the <see cref="HttpTestServer"/>.
/// </summary>
public sealed class HttpPathFixture : IPathSchemeFixture, IDisposable
{
    /// <summary>See class doc — mints a Context-wired HttpPath at a fresh server URL.</summary>
    public Task<Path> CreateFresh() => throw new NotImplementedException();

    /// <summary>See class doc — removes the server-side entry, idempotent.</summary>
    public Task Cleanup(Path p) => throw new NotImplementedException();

    /// <summary>The HTTP test server has no directory-listing concept — scope List out.</summary>
    public bool CanPerform(VerbName verb) => verb != VerbName.List;

    public string Scheme => "http";

    public void Dispose() => throw new NotImplementedException();
}
