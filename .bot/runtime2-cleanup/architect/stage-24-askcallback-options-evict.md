# Stage 24: `askcallback-options-evict`

**Read first:**
- `plan/principles.md` — OBP discipline. Rule C (static fields are a missing `@this`); smell #3 (same logical thing stored twice across types).
- `results.md` deviation #6 — coder's note on the four sites stage 16 deferred.

**Goal:** Evict the static `JsonSerializerOptions` field from `AskCallback.cs` to an instance-owned slot on `Callback.@this`. Discovery during carving: `ErrorCallback.cs` carries an identical static — same shape, same modifiers, same security purpose. Both move to one shared instance on `Callback.@this`. One realignment ("callback wire JSON options are owned by the callback subsystem"), two static fields evicted, one live slot.

## Scope expansion flagged

The Tier 5 one-liner named only `AskCallback.cs._options`. Reading the code while carving surfaced that `ErrorCallback.cs:163-175` carries a static `_options` that is byte-for-byte the same configuration — camelCase + case-insensitive + WhenWritingNull + `Filters.Sensitive.Strip` modifier. They are two declarations of one logical thing. Evicting only the AskCallback copy would leave a duplicate Rule C site that is clearly the same realignment. **This brief takes the expanded scope.** If you'd rather split it, say — but the architect's call is "one realignment, both copies" because smell #3 (same logical thing stored twice across types) is the same root as Rule C here, and walking past a known duplicate while fixing its twin is worse than fixing both.

## Scope

**Included:**
- Add an instance-owned `WireOptions` slot on `App.Callback.@this` (initialised at construction).
- Drop `AskCallback._options` static (file `AskCallback.cs:106-117`); both callers (Serialize, Deserialize) read `ctx.App.Callback.WireOptions` instead.
- Drop `ErrorCallback._options` static (file `ErrorCallback.cs:163-175`); both callers (private static `SerializeSnapshot` / `DeserializeSnapshot`, called from `Serialize` / `Deserialize`) take the options through.
- Test sweep — both `AskCallbackTests` and `ErrorCallbackTests` already round-trip through `Serialize(ctx)` / `Deserialize(bytes, ctx)`, so they pick up the new path with no changes. Verify after rebuild.

**Excluded:**
- Any change to wire format, encryption flow, size caps, or the property/field shapes on either callback.
- Pulling the encrypt/decrypt round-trip duplication out of Serialize/Deserialize — see "Out of scope, but noted" below.
- Splitting `Callback.@this` into a `Callback/Wire/this.cs` sub-@this — same place, see "Out of scope, but noted."

## Deliverables

### New slot on `Callback.@this`

`PLang/App/Callback/this.cs` gains:

```csharp
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;

namespace App.Callback;

public sealed class @this
{
    public Signature.@this Signature { get; } = new();

    /// <summary>
    /// JsonSerializerOptions used by both AskCallback and ErrorCallback for wire
    /// (de)serialization. JsonSerializerOptions becomes cache-frozen on first use, so
    /// one app-scoped instance is the canonical reuse pattern. The Filters.Sensitive
    /// modifier strips [Sensitive]-marked properties from the wire — captured Variables
    /// can carry arbitrary objects whose typed properties may include secrets
    /// (Security v1 S-F4).
    /// </summary>
    internal JsonSerializerOptions WireOptions { get; } = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        TypeInfoResolver = new DefaultJsonTypeInfoResolver
        {
            Modifiers = { App.Channels.Serializers.Filters.Sensitive.Strip }
        }
    };
}
```

Visibility: `internal` is right — only AskCallback and ErrorCallback should reach this. Public would invite reuse outside the callback subsystem, which is not intended.

### AskCallback.cs — drop static, reach via context

```csharp
// Line 34 — Serialize:
var bytes = JsonSerializer.SerializeToUtf8Bytes(wire, ctx.App.Callback.WireOptions);

// Line 62 — Deserialize:
var wire = JsonSerializer.Deserialize<Wire>(plain, ctx.App.Callback.WireOptions)
           ?? throw new InvalidOperationException("AskCallback: empty wire payload");

// Lines 106–117 — DELETE the entire `private static readonly JsonSerializerOptions _options = new() { ... };` block.
```

The two top-of-file usings (`System.Text.Json`, `System.Text.Json.Serialization.Metadata`) stay only if other code in the file still needs them; sweep at the end and clean unused.

### ErrorCallback.cs — drop static, thread options through helpers

The private static helpers `SerializeSnapshot(Snapshot.@this s)` and `DeserializeSnapshot(byte[] bytes)` currently use `_options` directly. They're called once each — from `Serialize(ctx)` and `Deserialize(bytes, ctx)`. The cleanest move is to thread the options as a parameter so the helpers stay private static (they're behaviour, not state):

```csharp
// Caller side — line 38 (Serialize):
var bytes = SerializeSnapshot(AppSnapshot, ctx.App.Callback.WireOptions);

// Caller side — line 63 (Deserialize):
var snap = DeserializeSnapshot(plain, ctx.App.Callback.WireOptions);

// Helper signatures change:
private static byte[] SerializeSnapshot(Snapshot.@this s, JsonSerializerOptions options) { ... uses options ... }
private static Snapshot.@this DeserializeSnapshot(byte[] bytes, JsonSerializerOptions options) { ... uses options ... }

// Lines 163–175 — DELETE the entire `private static readonly System.Text.Json.JsonSerializerOptions _options = new() { ... };` block.
```

Alternatively, inline the helpers into `Serialize` / `Deserialize` directly (they're only one call site each). Coder's choice — the parameter-thread version preserves the current factor; the inline version flattens it. Either is fine; the static-eviction is what matters.

### Caller propagation

External callers of `_options` on either callback: zero. The static is private; nothing outside the file reaches it. The eviction touches only the two callback files plus the new slot on `Callback.@this`.

External tests: zero changes. Both `AskCallbackTests` and `ErrorCallbackTests` round-trip through `Serialize(ctx)` / `Deserialize(bytes, ctx)` and read no internal options. The context already exists; the navigation `ctx.App.Callback.WireOptions` works for them transparently.

### Definition of done

- `dotnet build PlangConsole` clean.
- `dotnet run --project PLang.Tests` green (baseline 2752/2752).
- `cd Tests && ../PlangConsole/bin/Debug/net10.0/plang --tester` green from a fresh rebuild.
- `grep -n "private static readonly JsonSerializerOptions\|private static readonly System.Text.Json.JsonSerializerOptions" PLang/App/Callback/AskCallback.cs PLang/App/Callback/ErrorCallback.cs` — zero hits.
- `grep -n "WireOptions" PLang/App/Callback/this.cs` — one declaration.
- `grep -n "ctx.App.Callback.WireOptions" PLang/App/Callback/AskCallback.cs PLang/App/Callback/ErrorCallback.cs` — used in both callbacks (4 sites total — Serialize + Deserialize on each).

**Dependencies:** None. Independent of every other Tier 5 stage.

## Design

### The smell this closes

Rule C — two static `JsonSerializerOptions` fields with no owner. They're allocated once at class init, immutable thereafter (JsonSerializerOptions becomes cache-frozen on first use), live for the process lifetime. Process-global state is exactly what Rule C calls out: data with no `@this` it belongs to.

Smell #3 reinforces the same finding: the two declarations are byte-for-byte identical. They're not "two unrelated configs that happen to look alike" — they're one logical thing duplicated across two types because the natural owner wasn't given to them at the time. Smell #3's prescription is the same as Rule C's here: hand the data to the type whose responsibility it is.

`Callback.@this` is the right owner. It is already the app-scoped configuration holder for the callback subsystem (see its current docstring at `PLang/App/Callback/this.cs:3-10`). Adding `WireOptions` extends the same role with one more piece of subsystem-wide config.

### Why one shared `WireOptions` instead of two

Once allocated, `JsonSerializerOptions` is reused indefinitely — the class is immutable after first serialization (.NET doc: "options become read-only on first serialization"). One instance is the canonical pattern, and at runtime the two callbacks already construct two byte-identical instances. Sharing one eliminates the duplication AND removes the risk that the next person editing `_options` updates one copy and forgets the other.

If a future change makes the two callbacks need different wire options (e.g. ErrorCallback wants different size handling), split them then. Today they don't.

### Why `Callback.@this`, not `Channels.Serializers`

The options ride the callback wire, not a generic serialization wire. `Filters.Sensitive.Strip` is the bridge to `Channels.Serializers.Filters` (the cross-cutting filter), but the options bag itself is a callback-subsystem concern. Putting it under `app.Callback` keeps the callback wire policy local to the callback subsystem; putting it under `app.Channels.Serializers` would imply this is general-purpose serializer config, which it isn't (the sensitive-strip + camelCase combination is specifically the callback wire format).

### Why instance, not `static readonly` on Callback.@this

Rule C's three exceptions for static state: `const`, `AsyncLocal<T>`, and irreducibly-static lock objects. JsonSerializerOptions doesn't fit any of them — it's allocated runtime state with no compile-time constness. The exception list is closed; "but JsonSerializerOptions is effectively read-only after first use" is not on it. Make it instance.

(Coder note: yes, this means *per-app* allocation rather than *per-process*. In practice, App is constructed once per CLI invocation; per-app vs per-process is the same physical lifetime. The OBP correctness matters more than the negligible allocation cost.)

### Out of scope, but noted

While reading the two callbacks, two further smells surface that this stage explicitly does NOT close:

1. **Wire serialization duplication.** AskCallback.Serialize and ErrorCallback.Serialize both: `serialize → encrypt(via RunAction) → return bytes`. Symmetric on Deserialize. The size-cap constants (`MaxWireBytes` 1MB / 4MB) and encrypt/decrypt round-trip are duplicated. A clean factor would be a `Callback/Wire/this.cs` @this that owns Serialize/Deserialize as a typed boundary — both callbacks delegate to it.

2. **Position and Action navigation in Run.** Both callbacks dispatch via `await ctx.App.Run(Position.Action, ctx)` (or `bottom.Action`). That's fine, but the "look up Position, then dispatch action" sequence is the same shape; a `Wire.Resume(ctx, action)` helper could absorb it.

Neither belongs in stage 24. Filed mentally as a follow-up "Wire @this" stage if appetite emerges; not added to Tier 5 unilaterally.

## Risk + dependencies

**Risk: low.** The behaviour is exactly preserved — same options, same shape, same Filters.Sensitive modifier. The only change is the storage location.

Possible failure modes:
1. **Initialization ordering.** `Callback.@this`'s `WireOptions` allocation calls into `App.Channels.Serializers.Filters.Sensitive.Strip` — verify that Filters loads before App finishes constructing Callback. Currently both `Callback.@this` and Filters are constructed eagerly during App boot; the access pattern is the same as today's `_options` static (which references the same modifier). No ordering surprise expected.
2. **A hidden static caller.** Both `_options` are private — `grep -n "_options" PLang/App/Callback/AskCallback.cs PLang/App/Callback/ErrorCallback.cs` already returns the only sites. Sweep before deletion to confirm.
3. **Test parallelism.** If multiple tests construct multiple Apps in parallel and each builds its own `WireOptions`, that's *more* allocation than today's process-static, not less. Allocation is cheap (the modifier is a delegate ref); not a functional concern.

**Dependencies: none.** Independent of stages 23 and 25–28.

## Watch for (coder eyes-on)

- **Both callbacks add `using App.Callback;` if not already present** — actually neither file does, since they're inside `namespace App.Callback`. The reach is `ctx.App.Callback` (already correctly typed). No new using.
- **The `internal` visibility of `WireOptions`.** Internal in PLang.dll suffices because both callback files live in PLang. Tests that don't access `WireOptions` directly don't need `InternalsVisibleTo`.
- **The unused-using sweep on AskCallback.cs after the static is gone.** `using System.Text.Json.Serialization.Metadata;` was only there for `DefaultJsonTypeInfoResolver` in the static block — once the static is deleted, the using becomes dead. Same on ErrorCallback.cs. Compiler warning may surface; clean it up.
- **`Callback.@this`'s field-init order.** `Signature` first, then `WireOptions`. Both get `= new()` so order is by source position only; no dependency.

## Out of scope

- Renaming `WireOptions` to anything else after coder lands — bikeshed in review, settle in this brief.
- The full `Callback/Wire/this.cs` @this carve (see "Out of scope, but noted").
- Anything in `Channels/Serializers/Filters/` itself.
- Any change to `Callback.@this.Signature` — that subsystem is OBP-correct as-is (see Tier 5 intro / `results.md` deviation #3).

## Commit plan

```
runtime2-cleanup stage 24: AskCallback._options + ErrorCallback._options → Callback.@this.WireOptions

Two static JsonSerializerOptions evicted into one instance-owned slot on
the callback subsystem's @this.

Both AskCallback and ErrorCallback held an identical private static
JsonSerializerOptions configured with camelCase + case-insensitive +
WhenWritingNull + Filters.Sensitive.Strip. Smell #3 (same logical
thing duplicated across types) made the two-static eviction one
realignment, not two.

After: app.Callback.WireOptions is the single home. Both callbacks
reach it via ctx.App.Callback.WireOptions in Serialize and
Deserialize. Behaviour unchanged — same options, same modifiers,
same wire format.

Files touched:
- PLang/App/Callback/this.cs — gains WireOptions property.
- PLang/App/Callback/AskCallback.cs — static deleted; two read sites updated.
- PLang/App/Callback/ErrorCallback.cs — static deleted; helpers thread options.

C# 2752/2752 + PLang 199/199 baseline preserved.

Tier 5 stage 24. Rule C (static fields are a missing @this) +
smell #3 (same logical thing stored twice across types) closure.
```
