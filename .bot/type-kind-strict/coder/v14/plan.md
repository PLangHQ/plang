# coder v14 plan — resolve auditor v1 F1 (lazy reference-fundamental never loads)

**Responds to:** auditor v1 (NEEDS WORK, Major F1).
**Decision owner:** Ingi (design call made in-session). This doc is the concrete
plan for architect ratification + coder implementation.

## The bug (one root cause, two symptoms)

`image.Convert` mints a **lazy path-backed handle** (`this.Convert.cs:24`) for
`set %img% = "x.png" as image[/kind [strict]]`. The only code that reads the
bytes from disk *and* runs the strict check is `image.BytesAsync()`
(`image/this.cs:116`) — and it has **zero production callers**. Every real
consumer reads the sync `.Bytes` getter (`image/this.cs:53`), which returns
`Array.Empty<byte>()` until `_bytes` is populated.

Consequences (same root — nothing triggers the async load):
- A path-backed image renders as **empty bytes** in every channel/format
  (`serializer/Default.cs`, `protobuf.cs`, `text.cs`, plus `Width`/`Height` and
  `ValidateKind`'s `_ => Bytes` fallback).
- `strict` is silently advisory — `StrictKindMismatchException` never fires.

## Why "make every consumer async" is the wrong fix

The byte read happens **inside an STJ converter callback**. The chain:

```
serializer.SerializeAsync          // async — entry (plang/this.cs:135, Json.cs:71)
  └ JsonSerializer.SerializeAsync  // async — STJ pump
  ──────────── HARD SYNC WALL ────────────
     └ Wire.Write(writer,data,opts)// SYNC — JsonConverter<@this>.Write (Wire.cs:438)
        └ data.Normalize(View)     // SYNC (Wire.cs:499)
           └ writer.Value(node)    // SYNC — TypedValueNode (json/writer.cs:135)
              └ delegate void Write // SYNC — renderer (renderer/this.cs:28)
                 └ value.Bytes      // empty
```

`JsonConverter<T>.Write` is sync by the System.Text.Json contract — no async
form. Once `JsonSerializer.SerializeAsync` enters the converter, nothing below
can `await`. Making the renderer surface async means abandoning STJ converters
and hand-rolling the wire writer — replacing the serialization engine, not a
mechanical change. The sync `.Bytes` getter is **correct** inside that sync
converter; the defect is that nobody pulls the bytes into memory *above* the
wall.

## The fix — `Data.Load()`: one async materialization pass above the sync wall

Lazy stays lazy through `set` and navigation. `Load()` is the single async
"pull into memory" moment, run just before the sync formatter starts. By the
time STJ enters `Wire.Write`, `_bytes` is populated and the sync renderers read
real bytes.

### 1. New marker interface — `app.data.ILoadable`

```csharp
namespace app.data;

/// <summary>
/// A value with lazy content that materialises through async I/O (a reference
/// fundamental: image; audio/video follow). LoadAsync pulls the content into
/// memory so subsequent SYNC readers (serializer renderers, Width/Height) see
/// real bytes. Idempotent: a value already in memory returns immediately.
/// The load seam is also where a strict reference fundamental throws
/// StrictKindMismatchException (see IStrictKindEnforcer).
/// </summary>
public interface ILoadable
{
    System.Threading.Tasks.Task LoadAsync();
}
```

`image.@this` implements it as `public Task LoadAsync() => BytesAsync();`
(discards the result — `BytesAsync` loads, caches into `_bytes`, and runs
`CheckStrictKind`). Note this is a **separate concern from `IStrictKindEnforcer`**:
the empty-bytes bug hits *all* path-backed images, strict or not, so the marker
is "has lazy content," not "is strict." Strict enforcement piggybacks because
`BytesAsync` is the throw site.

### 2. `Data.Load()` — async graph walk mirroring Normalize

New partial file `PLang/app/data/this.Load.cs`:

```csharp
public async Task Load()
{
    var visited = new HashSet<object>(ReferenceEqualityComparer.Instance);
    await LoadValue(Value, visited, depth: 0);
}
```

`LoadValue` recurses the **same shapes `NormalizeValue` walks** (cycle guard +
`MaxNormalizeDepth` cap, identical to `this.Normalize.cs`):
- nested `Data` → recurse `.Value`
- `IDictionary` → recurse each value
- `IEnumerable` (not `byte[]`/string) → recurse each item
- leaf: if `value is ILoadable l` → `await l.LoadAsync()`
- tree-native leaves (string/number/bool/etc.) → no-op

Idempotent and cheap when nothing is lazy (the common case): already-loaded
images return from `BytesAsync` on the `_bytes != null` fast path; a graph with
no `ILoadable` is a pure walk.

### 3. Call site — the serialize chokepoint

`await data.Load()` as the **first line** of each `ISerializer.SerializeAsync`
impl, before the STJ call:
- `PLang/app/channel/serializer/plang/this.cs:135`
- `PLang/app/channel/serializer/Json.cs:71`
- `Text.cs` delegates to the json fallback — covered transitively, but add it
  explicitly if Text ever renders directly.

The leaf impl is the un-bypassable point: every channel output **and** wire
egress (`this.Transport.cs:121`, registry dispatch `serializer/list/this.cs:161`)
goes through an `ISerializer.SerializeAsync`. Putting `Load()` in the registry
dispatch alone would miss the direct-instance Transport path; the leaf covers
both.

### 4. Strict error rides back out — no torn stream

`Load()` runs inside the serializer `try` (and the channel `WriteAsync`
try/catch, `channel/this.cs:124`). A `StrictKindMismatchException` (or a read
failure / denied path) thrown from `BytesAsync` is caught **before any bytes are
written to the stream**, and surfaces as a normal `Data` error
(`WriteError` / a dedicated `StrictKindMismatch` key) to the caller — the strict
enforcement that is missing today, for free.

### 5. Other byte-readers (off the sync wall)

`Width`/`Height` and `ValidateKind` are reached through already-async action
handlers, not the sync serializer. Those can `await BytesAsync()` directly at
their own call sites — separate, smaller follow-ups; not part of the serialize
fix. Flag for a sweep, don't fold in here.

## Tests

- **Goal-level (the seam the current tests punt on):**
  `set %img% = "fixtures/real.gif" as image/gif strict` → `write out %img%`
  → assert output is the real base64, **non-empty**. This is the
  consumer-driven flow auditor F1 says has no coverage.
- **Strict mismatch through the real flow:**
  `set %img% = "fixtures/real.png" as image/gif strict` → `write out %img%`
  → assert a `Data` error with the strict-mismatch key, **stream not written**.
- **Non-strict path-backed correctness:**
  `set %img% = "fixtures/real.png" as image` → `write out %img%` → non-empty
  base64 (proves the bug was broader than strict).
- **Idempotence / no-op:** a bytes-backed image and a path-free scalar graph
  round-trip unchanged through `Load()` (no extra reads).
- **Mutation check:** delete the `await data.Load()` line → the first two tests
  go red (empty output / no strict throw). Confirms the call site is load-bearing.

## Blast radius

- New: `app/data/ILoadable.cs`, `app/data/this.Load.cs`, `image.LoadAsync`.
- Edited: 2 (–3) `SerializeAsync` entries (one line each).
- No change to `Normalize`, the renderer delegate, the STJ converters, or the
  sync `.Bytes` getter. The serialization engine is untouched.
