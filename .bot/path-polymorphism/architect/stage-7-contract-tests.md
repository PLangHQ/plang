# Stage 7: `PathSchemeContractTests<T>`

**Goal:** A generic test base every scheme handler runs through. Verb round-trips, Permission gating, failure-shape uniformity — all asserted once, applied to every scheme. Lowers the bar for adding `S3Path`, `GitPath`, etc. in the future.

**Scope:** Generic test class, fixture interface, FilePath fixture, HttpPath fixture. Both schemes pass the same contract suite.

**Out of scope:**
- Tests for schemes that don't exist yet (S3, Git, etc.). The framework is here for them; they'll bring their own fixtures.
- "Skip if unsupported" semantics. The "let the server respond" rule means even unsupported verbs return `data.@this.Fail` — the test asserts the failure shape, not skips.

## Design

### Fixture interface

`PLang.Tests/app/types/PathTests/IPathSchemeFixture.cs`:

```csharp
public interface IPathSchemeFixture
{
    /// <summary>Mint a fresh Path that this scheme can write to. Returns a unique path each call.</summary>
    Task<Path> CreateFresh();

    /// <summary>Tear down the resource at this path. Idempotent; deleting an already-gone resource is fine.</summary>
    Task Cleanup(Path p);

    /// <summary>
    /// True if this scheme's underlying system can perform the verb at all
    /// (e.g. an HTTP server that returns 405 for POST is NOT "doesn't support" —
    /// 405 is a return value, not a contract failure).
    /// Used to scope which tests run; not used to skip assertions.
    /// </summary>
    bool CanPerform(VerbName verb);

    /// <summary>The scheme name this fixture provides (for diagnostic output).</summary>
    string Scheme { get; }
}
```

### Generic base

`PLang.Tests/app/types/PathTests/PathSchemeContractTests.cs`:

```csharp
public abstract class PathSchemeContractTests<TFixture>
    where TFixture : IPathSchemeFixture, new()
{
    protected TFixture Fixture { get; } = new();

    [Test]
    public async Task ReadText_Returns_What_WriteText_Wrote()
    {
        var p = await Fixture.CreateFresh();
        try
        {
            var content = $"contract test {Guid.NewGuid()}";
            var written = await p.WriteText(content);
            Assert.IsTrue(written.Success);
            var read = await p.ReadText();
            Assert.AreEqual(content, read.Value);
        }
        finally { await Fixture.Cleanup(p); }
    }

    [Test]
    public async Task Exists_Reflects_Lifecycle() { /* false → write → true → delete → false */ }

    [Test]
    public async Task Stat_Length_Matches_Written_Bytes() { /* ... */ }

    [Test]
    public async Task CopyTo_Same_Scheme_RoundTrips() { /* ... */ }

    [Test]
    public async Task CopyTo_Cross_Scheme_RoundTrips() { /* uses base default (ReadBytes + WriteBytes) */ }

    [Test]
    public async Task MoveTo_Is_CopyTo_Plus_Delete() { /* ... */ }

    [Test]
    public async Task Unauthorized_Read_Hits_Permission_Gate() { /* assert data.@this.Fail with PermissionDenied */ }

    [Test]
    public async Task Unauthorized_Write_Hits_Permission_Gate() { /* same */ }

    [Test]
    public async Task Failure_Shape_Is_Uniform() { /* the data.@this.Fail returned by an unauth read has the same Error.Type across schemes */ }
}
```

### Concrete tests

```csharp
public sealed class FilePathContractTests : PathSchemeContractTests<FilePathFixture> { }
public sealed class HttpPathContractTests : PathSchemeContractTests<HttpPathFixture> { }
```

### FilePath fixture

`PLang.Tests/app/types/PathTests/FilePathFixture.cs`:

```csharp
public sealed class FilePathFixture : IPathSchemeFixture
{
    private readonly string _root;
    public FilePathFixture()
    {
        _root = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"plang-contract-{Guid.NewGuid()}");
        Directory.CreateDirectory(_root);
    }

    public Task<Path> CreateFresh()
    {
        var file = System.IO.Path.Combine(_root, $"{Guid.NewGuid()}.txt");
        return Task.FromResult<Path>(new FilePath(file));
    }

    public Task Cleanup(Path p) { /* File.Delete if exists */ return Task.CompletedTask; }

    public bool CanPerform(VerbName v) => true;     // FS supports everything

    public string Scheme => "file";
}
```

(Note: `System.IO.Path` is the dotnet type, distinct from our `Path` global alias to `app.types.path.@this`. Be explicit at use sites to avoid ambiguity.)

### HttpPath fixture

`PLang.Tests/app/types/PathTests/HttpPathFixture.cs`:

In-process Kestrel server with a small handler that accepts GET/POST/DELETE/HEAD on `/contract/{guid}`. The handler stores written bytes in-memory keyed by guid; subsequent GET returns them; DELETE removes them.

```csharp
public sealed class HttpPathFixture : IPathSchemeFixture, IDisposable
{
    private readonly TestServer _server;
    private readonly ConcurrentDictionary<Guid, byte[]> _store = new();

    public HttpPathFixture() { /* spin up Kestrel with the verb-aware handler */ }

    public Task<Path> CreateFresh()
    {
        var guid = Guid.NewGuid();
        var url = $"{_server.BaseAddress}contract/{guid}";
        return Task.FromResult<Path>(new HttpPath(url));
    }

    public Task Cleanup(Path p) { /* DELETE via _server, or just clear _store entry */ return Task.CompletedTask; }

    public bool CanPerform(VerbName v) => v != VerbName.List;   // HTTP server doesn't implement directory listing here

    public string Scheme => "http";
    public void Dispose() => _server.Dispose();
}
```

### Cross-scheme assertions

The cross-scheme `CopyTo` test gets both fixtures, mints one Path from each, and asserts:

```csharp
[Test]
public async Task CopyTo_FilePath_To_HttpPath_Works()
{
    var src = await new FilePathFixture().CreateFresh();
    var dst = await new HttpPathFixture().CreateFresh();
    await src.WriteText("hello");
    var copied = await src.CopyTo(dst);
    Assert.IsTrue(copied.Success);
    var read = await dst.ReadText();
    Assert.AreEqual("hello", read.Value);
}
```

This proves the base `CopyTo` default works across schemes.

## Deliverables

- `IPathSchemeFixture` interface.
- `PathSchemeContractTests<TFixture>` generic base with ~9 contract assertions.
- `FilePathFixture` + `FilePathContractTests`.
- `HttpPathFixture` + `HttpPathContractTests`.
- Cross-scheme `CopyTo` test (in its own file, not in the generic base).

## Tests

This stage *is* tests. No new product code. The deliverable is the framework + first two concrete suites.

## Risk

Low. The contract assertions are well-defined. The risk is that one contract assertion is too strict for a future scheme — e.g., `Stat.Length` may not be available from every backing system. Mitigation: keep the contract list tight, only assert what every reasonable scheme can support. If a future scheme genuinely can't conform, that's the moment to revisit; don't pre-emptively soften.
