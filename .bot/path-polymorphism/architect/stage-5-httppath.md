# Stage 5: HttpPath

**Goal:** Land `HttpPath : Path` as the second scheme. Wire `http` and `https` into the registry at App startup. Prove polymorphism with a non-filesystem scheme.

**Scope:** HttpPath impl, identity wiring, App startup registration, error-shape semantics ("let the server respond"), Authorize wired internally per stage 3's pattern.

**Out of scope:**
- Permission per-scheme `Absolute` canonical-form (stage 6 — until then, HttpPath's `Absolute` returns a lightly-normalized Uri string).
- Contract test framework (stage 7).
- Bearer tokens, mTLS, custom auth — Settings-driven, scheme-internal, not on this branch.

## Design

### `HttpPath : Path`

`PLang/app/types/path/http/this.cs`:

```csharp
namespace app.types.path.http;

[PathScheme("http")]
[PathScheme("https")]
public sealed class @this : Path
{
    public @this(string raw) { /* parse, validate URI */ }

    public override string Raw => _raw;
    public override string Absolute => /* stage 6 canonical form; for now, lowercased Uri.ToString() */;
    public override string Scheme => _uri.Scheme;

    public override async Task<data.@this> ReadText()
    {
        var auth = await Authorize(new Verb { Read = new ReadVerb() });
        if (!auth.Success || auth.Type?.ClrType.Exit() == true) return auth;
        // GET → data.@this<string> or data.@this.Fail with status
    }

    public override async Task<data.@this> WriteText(string content) { /* Authorize + POST */ }
    public override async Task<data.@this> Delete() { /* Authorize + DELETE */ }
    public override async Task<data.@this> Stat()   { /* Authorize + HEAD */ }
    public override async Task<data.@this> Exists() { /* HEAD, 2xx → true, 4xx → false */ }
    public override async Task<data.@this> List()   { /* server-defined (autoindex or data.@this.Fail) */ }
    // ... ReadBytes, WriteBytes, Append, Save similarly ...

    private static readonly HttpClient _client = /* configured once */;
}
```

Add the `HttpPath` global alias in `PLang/app/GlobalUsings.cs`:

```csharp
global using HttpPath = app.types.path.http.@this;
```

The HttpClient is `static readonly` on the class — one client shared across all HttpPath instances within the process. This is the dotnet-recommended HttpClient lifecycle (connection pooling, DNS caching). Multi-App safe because it's stateless after init.

### Identity

PLang's built-in signing identity attaches to outgoing requests by default. Reuse whatever identity API the existing http module uses. The base `Path` class doesn't know about identity — `HttpPath` does, because identity-bearing-on-HTTP is a scheme-specific concern.

**Decision:** identity is automatic for `HttpPath` — every request signed with PLang identity headers. Developers needing different credentials reach into `Settings` from inside their own scheme handler (a future `MyAuthHttpPath` they register over `http`).

### Error shape — "let the server respond"

Non-2xx responses do **not** throw. They become `data.@this.Fail` with the status code in the error payload:

- 404 → `data.@this.Fail` with `Error.Type = "NotFound"`, status `404`.
- 405 → `data.@this.Fail` with `Error.Type = "MethodNotAllowed"`, status `405`. (POST to a GET-only endpoint is the example from the source doc.)
- 500-class → `data.@this.Fail` with status; the PLang program can decide via `on error`.

Network failures (DNS, connection refused, timeout) → `data.@this.Fail` with `Error.Type = "NetworkError"`.

PLang programs use `on error` to differentiate via the error type/status. Don't model HTTP errors as exceptions — they're return values per the doc's design.

### App-start wiring

In the App init code from stage 2:

```csharp
app.Types.Scheme.Register("file",  raw => new FilePath(raw));
app.Types.Scheme.Register("http",  raw => new HttpPath(raw));
app.Types.Scheme.Register("https", raw => new HttpPath(raw));
```

### Action handlers — no change

The whole point of polymorphism: `read.cs`, `save.cs`, etc. are already one-liners over `Path.Value!.X()` from stage 3. They don't know about HttpPath. A user writing:

```plang
- read https://api.example.com/users.json, write to %users%
```

… runs the same handler. Path dispatch happens at parameter resolution. Stage 3 work pays out here.

## Deliverables

- `PLang/app/types/path/http/this.cs` — the HttpPath class with all verb overrides.
- App startup additions for http/https registration.
- Identity wiring — reuse existing signing API, don't introduce a new one.
- `PLang/app/GlobalUsings.cs` — adds `global using HttpPath = app.types.path.http.@this;`.

## Tests

See `plan-test-designer.md` Stage 5. Key surfaces:

- In-process Kestrel test server.
- GET, POST, DELETE, HEAD round-trips.
- 405 → `data.@this.Fail(405)` (the design-doc rule).
- Identity headers present.
- Network failure → `data.@this.Fail` with `Error.Type = "NetworkError"`.
- Two consecutive calls reuse the same HttpClient (assert by behavior under load, not by direct field inspection).

## Risk

Moderate. Network I/O in tests is flaky if anything escapes the in-process server; pin Kestrel to a free port and use it from a fixture. The identity wiring needs to interop with the existing identity surface — if that surface is awkward, push back and design a small adapter rather than reaching into Settings raw.
