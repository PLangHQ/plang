# Stage 24: `askcallback-options-evict`

**Read first:**
- `plan/principles.md` — OBP discipline. Rule A (compound class/property names), Rule C (static fields are a missing `@this`); smell #3 (same logical thing stored twice across types).
- `results.md` deviation #6 — coder's note on the four sites stage 16 deferred.

**Goal:** Evict the static `JsonSerializerOptions` field from `AskCallback.cs` to an instance-owned slot on a new `Callback.Wire.@this` (subfolder, mirrors the `Callback.Signature.@this` shape). Discovery during carving: `ErrorCallback.cs` carries an identical static — same shape, same modifiers, same security purpose. Both move to one shared instance on `Callback.Wire`. One realignment ("callback wire JSON options have an OBP-correct owner"), two static fields evicted, one live slot.

**OBP correction (architect 2026-05-09).** First draft of this brief proposed `Callback.@this.WireOptions` as a flat property. That's a Rule A violation — `WireOptions` is a compound name (Wire + Options) jamming owner+capability into a single property. The correct shape is a `Callback/Wire/this.cs` subfolder with `Options` as the property, navigated as `app.Callback.Wire.Options` — same shape as `app.Callback.Signature.Expires`. Brief revised to propose the subfolder shape from the start.

## Scope expansion flagged

The Tier 5 one-liner named only `AskCallback.cs._options`. Reading the code while carving surfaced that `ErrorCallback.cs:163-175` carries a static `_options` that is byte-for-byte the same configuration — camelCase + case-insensitive + WhenWritingNull + `Filters.Sensitive.Strip` modifier. They are two declarations of one logical thing. Evicting only the AskCallback copy would leave a duplicate Rule C site that is clearly the same realignment. **This brief takes the expanded scope.** If you'd rather split it, say — but the architect's call is "one realignment, both copies" because smell #3 (same logical thing stored twice across types) is the same root as Rule C here, and walking past a known duplicate while fixing its twin is worse than fixing both.

## Scope

**Included:**
- Create new `App/Callback/Wire/this.cs` subfolder + `@this`. One property: `Options` (instance-owned `JsonSerializerOptions`).
- Add a `Wire` property on `Callback.@this` (alongside the existing `Signature` property).
- Drop `AskCallback._options` static (file `AskCallback.cs:106-117`); both callers (Serialize, Deserialize) read `ctx.App.Callback.Wire.Options` instead.
- Drop `ErrorCallback._options` static (file `ErrorCallback.cs:163-175`); both callers (private static `SerializeSnapshot` / `DeserializeSnapshot`, called from `Serialize` / `Deserialize`) take the options through.
- Test sweep — both `AskCallbackTests` and `ErrorCallbackTests` already round-trip through `Serialize(ctx)` / `Deserialize(bytes, ctx)`, so they pick up the new path with no changes. Verify after rebuild.

**Excluded:**
- Any change to wire format, encryption flow, size caps, or the property/field shapes on either callback.
- Pulling the encrypt/decrypt round-trip duplication into `Wire.@this` — possible follow-up; see "Out of scope, but noted" below.
- Adding any property to `Wire.@this` other than `Options` — keep this stage minimal.

## Deliverables

### New `Callback/Wire/this.cs`

Create the new file `PLang/App/Callback/Wire/this.cs`:

```csharp
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;

namespace App.Callback.Wire;

/// <summary>
/// Wire-format configuration for the callback subsystem. Holds the JsonSerializerOptions
/// used by both AskCallback and ErrorCallback for wire (de)serialization. JsonSerializerOptions
/// becomes cache-frozen on first use, so one app-scoped instance is the canonical reuse pattern.
/// The Filters.Sensitive modifier strips [Sensitive]-marked properties from the wire —
/// captured Variables can carry arbitrary objects whose typed properties may include secrets
/// (Security v1 S-F4).
///
/// Read as <c>app.Callback.Wire.Options</c>.
/// </summary>
public sealed class @this
{
    internal JsonSerializerOptions Options { get; } = new()
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

Visibility on `Options`: `internal` is right — only AskCallback and ErrorCallback should reach it. Public would invite reuse outside the callback subsystem, which is not intended.

### Mount on `Callback.@this`

`PLang/App/Callback/this.cs` gains a `Wire` property alongside the existing `Signature`:

```csharp
namespace App.Callback;

public sealed class @this
{
    public Signature.@this Signature { get; } = new();
    public Wire.@this Wire { get; } = new();
}
```

The shape mirrors the existing `Signature` subfolder exactly. Same OBP discipline (subfolder + `@this` + navigation chain) for both subsystems.

### AskCallback.cs — drop static, navigate to Wire.Options

```csharp
// Line 34 — Serialize:
var bytes = JsonSerializer.SerializeToUtf8Bytes(wire, ctx.App.Callback.Wire.Options);

// Line 62 — Deserialize:
var wire = JsonSerializer.Deserialize<Wire>(plain, ctx.App.Callback.Wire.Options)
           ?? throw new InvalidOperationException("AskCallback: empty wire payload");

// Lines 106–117 — DELETE the entire `private static readonly JsonSerializerOptions _options = new() { ... };` block.
```

The two top-of-file usings (`System.Text.Json`, `System.Text.Json.Serialization.Metadata`) stay only if other code in the file still needs them; sweep at the end and clean unused.

### ErrorCallback.cs — drop static, thread options through helpers

The private static helpers `SerializeSnapshot(Snapshot.@this s)` and `DeserializeSnapshot(byte[] bytes)` currently use `_options` directly. They're called once each — from `Serialize(ctx)` and `Deserialize(bytes, ctx)`. The cleanest move is to thread the options as a parameter so the helpers stay private static (they're behaviour, not state):

```csharp
// Caller side — line 38 (Serialize):
var bytes = SerializeSnapshot(AppSnapshot, ctx.App.Callback.Wire.Options);

// Caller side — line 63 (Deserialize):
var snap = DeserializeSnapshot(plain, ctx.App.Callback.Wire.Options);

// Helper signatures change:
private static byte[] SerializeSnapshot(Snapshot.@this s, JsonSerializerOptions options) { ... uses options ... }
private static Snapshot.@this DeserializeSnapshot(byte[] bytes, JsonSerializerOptions options) { ... uses options ... }

// Lines 163–175 — DELETE the entire `private static readonly System.Text.Json.JsonSerializerOptions _options = new() { ... };` block.
```

Alternatively, inline the helpers into `Serialize` / `Deserialize` directly (they're only one call site each). Coder's choice — the parameter-thread version preserves the current factor; the inline version flattens it. Either is fine; the static-eviction is what matters.

### Caller propagation

External callers of `_options` on either callback: zero. The static is private; nothing outside the file reaches it. The eviction touches only the two callback files plus the new `Wire/this.cs` and the new `Wire` property on `Callback.@this`.

External tests: zero changes. Both `AskCallbackTests` and `ErrorCallbackTests` round-trip through `Serialize(ctx)` / `Deserialize(bytes, ctx)` and read no internal options. The context already exists; the navigation `ctx.App.Callback.Wire.Options` works for them transparently.

### Definition of done

- `dotnet build PlangConsole` clean.
- `dotnet run --project PLang.Tests` green (baseline 2752/2752).
- `cd Tests && ../PlangConsole/bin/Debug/net10.0/plang --tester` green from a fresh rebuild.
- `grep -n "private static readonly JsonSerializerOptions\|private static readonly System.Text.Json.JsonSerializerOptions" PLang/App/Callback/AskCallback.cs PLang/App/Callback/ErrorCallback.cs` — zero hits.
- `find PLang/App/Callback/Wire/this.cs` — present (new file).
- `grep -n "Wire " PLang/App/Callback/this.cs` — `Wire` property declared.
- `grep -n "ctx.App.Callback.Wire.Options" PLang/App/Callback/AskCallback.cs PLang/App/Callback/ErrorCallback.cs` — used in both callbacks (4 sites total — Serialize + Deserialize on each).

**Dependencies:** None. Independent of every other Tier 5 stage.

## Design

### The smell this closes

Rule C — two static `JsonSerializerOptions` fields with no owner. They're allocated once at class init, immutable thereafter (JsonSerializerOptions becomes cache-frozen on first use), live for the process lifetime. Process-global state is exactly what Rule C calls out: data with no `@this` it belongs to.

Smell #3 reinforces the same finding: the two declarations are byte-for-byte identical. They're not "two unrelated configs that happen to look alike" — they're one logical thing duplicated across two types because the natural owner wasn't given to them at the time. Smell #3's prescription is the same as Rule C's here: hand the data to the type whose responsibility it is.

`Callback.@this` is the right owner. It is already the app-scoped configuration holder for the callback subsystem (see its current docstring at `PLang/App/Callback/this.cs:3-10`). Adding `WireOptions` extends the same role with one more piece of subsystem-wide config.

### Why one shared `WireOptions` instead of two

Once allocated, `JsonSerializerOptions` is reused indefinitely — the class is immutable after first serialization (.NET doc: "options become read-only on first serialization"). One instance is the canonical pattern, and at runtime the two callbacks already construct two byte-identical instances. Sharing one eliminates the duplication AND removes the risk that the next person editing `_options` updates one copy and forgets the other.

If a future change makes the two callbacks need different wire options (e.g. ErrorCallback wants different size handling), split them then. Today they don't.

### Why `Callback/Wire/`, not `Channels.Serializers`

The options ride the callback wire, not a generic serialization wire. `Filters.Sensitive.Strip` is the bridge to `Channels.Serializers.Filters` (the cross-cutting filter), but the options bag itself is a callback-subsystem concern. Putting it under `app.Callback.Wire` keeps the callback wire policy local to the callback subsystem; putting it under `app.Channels.Serializers` would imply this is general-purpose serializer config, which it isn't (the sensitive-strip + camelCase combination is specifically the callback wire format).

### Why a `Wire/` subfolder, not a flat property

A flat property (`Callback.@this.WireOptions`) would compound owner+capability into a single property name — Rule A violation. The right shape is the navigation chain `app.Callback.Wire.Options`, mirroring `app.Callback.Signature.Expires` exactly. A subfolder with a single-property `@this` is OBP-correct (size of the type doesn't determine correctness; navigation shape does). Same insight that withdrew the planned `Callback/Signature/` collapse from Tier 5.

### Why instance, not `static readonly` on Wire.@this

Rule C's three exceptions for static state: `const`, `AsyncLocal<T>`, and irreducibly-static lock objects. JsonSerializerOptions doesn't fit any of them — it's allocated runtime state with no compile-time constness. The exception list is closed; "but JsonSerializerOptions is effectively read-only after first use" is not on it. Make it instance.

(Coder note: yes, this means *per-app* allocation rather than *per-process*. In practice, App is constructed once per CLI invocation; per-app vs per-process is the same physical lifetime. The OBP correctness matters more than the negligible allocation cost.)

### Out of scope, but noted

Once `Callback/Wire/` exists as a real `@this`, two further smells become natural to absorb into it later — but NOT in this stage:

1. **Wire serialization duplication.** AskCallback.Serialize and ErrorCallback.Serialize both: `serialize → encrypt(via RunAction) → return bytes`. Symmetric on Deserialize. The size-cap constants (`MaxWireBytes` 1MB / 4MB) and encrypt/decrypt round-trip are duplicated. `Wire.@this` is the natural future home for these — Serialize/Deserialize as typed methods, with both callbacks delegating to it.

2. **Position and Action navigation in Run.** Both callbacks dispatch via `await ctx.App.Run(Position.Action, ctx)` (or `bottom.Action`). The "look up Position, then dispatch action" sequence is the same shape; a `Wire.Resume(ctx, action)` helper could absorb it.

Neither belongs in stage 24 — they're separate ownership realignments. Stage 24 *creates* `Wire.@this` and gives it the `Options` property; later stages can extend it. Listed here so the design intent is captured: `Wire.@this` will likely grow.

## Risk + dependencies

**Risk: low.** The behaviour is exactly preserved — same options, same shape, same Filters.Sensitive modifier. The only change is the storage location.

Possible failure modes:
1. **Initialization ordering.** `Wire.@this`'s `Options` allocation calls into `App.Channels.Serializers.Filters.Sensitive.Strip` — verify that Filters loads before Callback finishes constructing Wire. Currently both `Callback.@this` and Filters are constructed eagerly during App boot; the access pattern is the same as today's `_options` static (which references the same modifier). No ordering surprise expected.
2. **A hidden static caller.** Both `_options` are private — `grep -n "_options" PLang/App/Callback/AskCallback.cs PLang/App/Callback/ErrorCallback.cs` already returns the only sites. Sweep before deletion to confirm.
3. **Test parallelism.** If multiple tests construct multiple Apps in parallel and each builds its own `Wire.Options`, that's *more* allocation than today's process-static, not less. Allocation is cheap (the modifier is a delegate ref); not a functional concern.

**Dependencies: none.** Independent of stages 23 and 25–28.

## Watch for (coder eyes-on)

- **Namespace for the new file**: `namespace App.Callback.Wire;` — same convention as `namespace App.Callback.Signature;`.
- **Mount on `Callback.@this`**: `public Wire.@this Wire { get; } = new();` — same shape as the existing `Signature` property. Field-init order: `Signature` then `Wire` (no dependency between them).
- **The `internal` visibility of `Wire.Options`.** Internal in PLang.dll suffices because both callback files live in PLang. Tests that don't access `Options` directly don't need `InternalsVisibleTo`.
- **The unused-using sweep on AskCallback.cs after the static is gone.** `using System.Text.Json.Serialization.Metadata;` was only there for `DefaultJsonTypeInfoResolver` in the static block — once the static is deleted, the using becomes dead. Same on ErrorCallback.cs. Compiler warning may surface; clean it up.
- **OBP shape parity check.** After landing, `app.Callback.Signature.Expires` and `app.Callback.Wire.Options` should read symmetrically — one navigation per dot, no compound names. If the brief surfaces a temptation to flatten ("just put it on Callback directly"), revisit Rule A and the OBP correction note above.

## Out of scope

- Renaming `WireOptions` to anything else after coder lands — bikeshed in review, settle in this brief.
- The full `Callback/Wire/this.cs` @this carve (see "Out of scope, but noted").
- Anything in `Channels/Serializers/Filters/` itself.
- Any change to `Callback.@this.Signature` — that subsystem is OBP-correct as-is (see Tier 5 intro / `results.md` deviation #3).

## Commit plan

```
runtime2-cleanup stage 24: AskCallback + ErrorCallback _options → Callback.Wire.Options

Two static JsonSerializerOptions evicted into one instance-owned slot
on a new Callback.Wire.@this subfolder.

Both AskCallback and ErrorCallback held an identical private static
JsonSerializerOptions configured with camelCase + case-insensitive +
WhenWritingNull + Filters.Sensitive.Strip. Smell #3 (same logical
thing duplicated across types) made the two-static eviction one
realignment, not two.

OBP shape: Callback/Wire/this.cs subfolder mirrors Callback/Signature/
exactly. Navigation is app.Callback.Wire.Options — the natural
extension of app.Callback.Signature.Expires. A flat property
(WireOptions) would have been a Rule A compound-name violation.

After: both callbacks reach options via ctx.App.Callback.Wire.Options
in Serialize and Deserialize. Behaviour unchanged — same options,
same modifiers, same wire format.

Files touched:
- PLang/App/Callback/Wire/this.cs — NEW. Holds Options property.
- PLang/App/Callback/this.cs — gains Wire property.
- PLang/App/Callback/AskCallback.cs — static deleted; two read sites updated.
- PLang/App/Callback/ErrorCallback.cs — static deleted; helpers thread options.

C# 2752/2752 + PLang 199/199 baseline preserved.

Tier 5 stage 24. Rule C (static fields are a missing @this) +
smell #3 (same logical thing stored twice across types) closure.
Wire/@this is now the natural future home for Serialize/Deserialize
+ size-cap dedup across both callbacks (out of scope this stage).
```
