# Stage 3: Data Lazy Signing + Per-Mimetype Serializers

**Goal:** Move signing onto `Data.@this` as a lazy property, introduce `Serializer.@this` per mimetype family, and have channels route by mimetype. After this stage, any Data crossing IO knows how to sign itself transparently when a serializer reads its `Signature`; debug/value-only paths pay zero crypto cost.
**Scope:** *Included* — `Data.@this` carries `Context`, lazy `Signature` property; `Serializer.@this` interface + `JsonSerializer` + `PlangDataSerializer`; `application/plang+data` MIME registration; channels dispatch to the right serializer; `App.modules.signing.SignedData` → `Signature` rename. *Excluded* — callback records (Stage 4), `application/plang+json` changes (already exists, reuse), file-sidecar shape (out of scope per `plan/transparent-signing.md`), DB-column-typed-as-Data path (acknowledge in serializer registry, leave for follow-up).
**Deliverables:**
- `Data.@this` constructor accepts `Context.@this` (or whatever carrier holds `App` reference); stored as a private field.
- `Data.@this.Signature` is a lazy property: first access populates via `signing.SignAsync(this, expiresInMs)`. `expiresInMs` is read from `Context.App.Callback.Signature.ExpiresInMs` *only when the wrapped value is `ICallback`*; null otherwise. Default `null`.
- `Serializer.@this` — base class or interface for per-mimetype serializers. Owns `Write(Data data, Stream stream, Context ctx)` and `Read(Stream stream, Context ctx) → Data`. The mimetype family the serializer handles is its own knowledge.
- `JsonSerializer : Serializer.@this` — handles `text/html`, `application/json`. Emits `data.Value` only; never reads `data.Signature`.
- `PlangDataSerializer : Serializer.@this` — handles `application/plang+data`. Emits the full envelope: Type + Value + Signature. Reads `data.Signature` (triggering lazy signing if not yet populated).
- `application/plang+data` MIME type registered in whatever the codebase's MIME registry is (or introduced if there isn't one yet).
- Channel.@this picks the right serializer based on the receiver's accept/mimetype and delegates. Channels never read `data.Signature` themselves.
- Rename `App.modules.signing.SignedData` to `Signature` (existing OBP cleanup queued in `plan/signature-rename.md`).
- C# tests: serializing the same Data through both serializers and asserting the wire shape; lazy signing only triggers when reading `.Signature`; `app.Callback.Signature.ExpiresInMs` propagates only for ICallback values; rename compiles cleanly across all callsites.
**Dependencies:** None structurally — could land in parallel with Stages 1 and 2. Touches every `Data` construction site in the codebase, so plan accordingly.

## Design

Three classes, three concerns:

```
Channel.@this
└── Picks Serializer for receiver's mimetype, calls Serializer.Write(data, stream, ctx).

Serializer.@this — one per mimetype family
├── JsonSerializer       (text/html, application/json)         → emits data.Value only
├── PlangDataSerializer  (application/plang+data)              → emits full envelope incl. Signature
└── Each owns its mimetype's wire shape. Reading data.Signature triggers signing on Data.

Data.@this
├── Value, Type, Signature
├── Signature is a lazy property — first read populates via signing module
└── Holds Context internally (set on construction)
```

`Data` signs itself; mimetype lives on serializers; channels orchestrate. No `PrepareForOutput` verb, no Channel-side hook ordering.

### The big decision: how does Data carry Context?

This stage is the only place in the branch where the OBP-pure shape and the engineering cost diverge enough to be worth flagging up front. Two paths:

- **(a) Constructor change.** `Data(Value, Context)` becomes the canonical constructor; existing callsites get touched, the type-system pins the discipline. Every place that builds a Data without context becomes a compile error — surfaces every bare construction at once.
- **(b) Additive.** Leave existing constructors; add `WithContext(ctx)` that callers invoke when they care about signing. Risk: bare Data with no context exists, and the lazy `Signature` getter has nothing to read from — has to throw or no-op.

I lean **(a)**. (b) creates two flavors of Data (with-context, without-context); the type system can't tell them apart; signing becomes "did anyone remember to call `WithContext`" — which is exactly the orchestration smell we're trying to remove. Constructor change is a deliberate one-time touch across the codebase; the resulting type-safety pays back forever.

The coder should confirm with the architect/Ingi before starting; this is an architectural fork that affects every Data callsite. Don't pick unilaterally.

### Lazy `Signature` getter shape

```csharp
public Signature.@this Signature
{
    get
    {
        if (_signature is null)
            _signature = signing.SignAsync(this, ExpiresInMs()).GetAwaiter().GetResult();
        return _signature;
    }
}

private int? ExpiresInMs() =>
    Value is ICallback
        ? _context.App.Callback.Signature.ExpiresInMs
        : null;
```

Sync-over-async in a property is a sketch; the real implementation may need an explicit `EnsureSignatureAsync()` and the property is `Signature => _signature` after that. Coder picks; the contract is "first access produces a populated Signature."

Once populated, the cached `_signature` is what's returned forever. Same Data, same signature. No re-signing.

### Per-mimetype serializers

Each `Serializer.@this` is `@this` for its mimetype family. There's no central `if (mimetype == X) emit X-style; else if ...` — that would be the centralized-translator smell. Each serializer owns its wire shape end-to-end.

`JsonSerializer.Write(data, stream, ctx)` calls some JSON encoder over `data.Value`. Does not touch `data.Signature`. Does not touch `data.Type` (mimetype is JSON; type info isn't part of the wire shape).

`PlangDataSerializer.Write(data, stream, ctx)` writes the full envelope. Reads `data.Type`, `data.Value`, `data.Signature` (which triggers signing). The exact binary layout is the coder's call — could be CBOR, length-prefixed JSON, custom — pick what's already used in the codebase if there's precedent. The interop story matters more than the binary shape: anything that reads `application/plang+data` is on the same side of the codebase, so changing the layout later is a controlled migration.

### Channel.@this — picking the serializer

Channels need a way to map receiver-mimetype → serializer. Three shapes:

- **Per-channel:** each Channel instance knows which serializer it uses (e.g. an HTTP channel reads the `Accept` header; a console channel reads its own startup config).
- **Registry:** `Serializers.GetByMimeType(string)` returns the matching serializer.
- **Both:** a registry exists; channels look up by mimetype.

Existing codebase pattern: there's already a `Serializers` collection in `App.Channels.Serializers`. Extend that — register `JsonSerializer` and `PlangDataSerializer` there at App boot. Channels look up by mimetype.

### `SignedData` → `Signature` rename

`App.modules.signing.SignedData` becomes `App.modules.signing.Signature` (or `Signature.@this` if it gets folded into a `Signature/` folder). This is OBP cleanup independent of the rest — `SignedData` was the wrong name (it's a signature, not data-that-was-signed). See `plan/signature-rename.md`.

### OBP smells to avoid

- *Don't pass `mimetype` through to Data.* Data doesn't know mimetypes exist; serializers do.
- *Don't add a `data.SignWithExpiry(ms)` method.* The expiry config is read from the App's config tree, not pushed in by callers. The lazy getter reads what it needs.
- *Don't add a `Channel.SignAndWrite(data)` overload.* Channels never sign. Channels pick serializers; serializers shape the wire.
- *Don't make `Signature` populating synchronous-by-blocking-async if it forces lazy access in hot paths.* If perf testing shows the sync-over-async wrapper is a problem, expose an `EnsureSignatureAsync` method that serializers `await` before reading.

### Test shape

- **Round-trip Json path:** build `Data<string>("hello", ctx)`, write through `JsonSerializer`, read back. Assert wire is just the JSON-encoded value; assert `data.Signature` was never populated (verify the lazy field stayed null).
- **Round-trip PlangData path:** same Data, write through `PlangDataSerializer`. Assert wire includes Type + Value + Signature. Read back; assert reconstructed Data has Signature populated (unverified) and Value matches.
- **Lazy expiry for ICallback:** wrap a fake `ICallback` in Data, set `app.Callback.Signature.ExpiresInMs = 60000`, write through PlangDataSerializer, assert `data.Signature.Expires` is roughly `now + 60s`. Repeat with the same Data wrapping a non-ICallback value: `Expires` is null even though config is set.
- **Rename:** existing tests that referenced `SignedData` should compile after rename.
- **Channel routing:** Channel handed Data + mimetype dispatches to the right serializer (mock both, assert correct one is called).

These are tight C# tests pinning the contract for Stage 4 to consume.
