# Stage 25: `http-default-statics-evict`

**Read first:**
- `plan/principles.md` — OBP discipline. Rule C (static fields are a missing `@this`).
- Stage 24 brief — same realignment pattern (JsonSerializerOptions eviction); use as a reference for the "options become instance" idiom.

**Goal:** Evict the two static `JsonSerializerOptions` fields in `App/modules/http/code/Default.cs`. The eviction takes two different shapes, one per field, because the two statics are *not* the same kind of thing on closer inspection.

## Carving discovery — the two statics aren't symmetrical

The Tier 5 one-liner said "evict `_jsonOptions` + `_transportInOptions` — same shape as stage 24." Reading the code surfaced that they aren't:

- **`_transportInOptions`** (`Default.cs:48-57`) — full local options block. CamelCase + case-insensitive + WhenWritingNull + `Filters.Transport.ForInbound` modifier. HTTP-transport-specific configuration. **This is the real Rule C target — instance-evict it onto `Default`.**

- **`_jsonOptions`** (`Default.cs:46`) — degenerate alias: `_jsonOptions = App.Utils.Json.CaseInsensitiveRead`. A value-copy of an external static; adds zero configuration of its own. Its two read sites (lines 397, 527) become direct references to `App.Utils.Json.CaseInsensitiveRead` — matching what line 646 in this same file already does. **The right Rule C fix is to delete it, not relocate it.** The underlying static (`Utils.Json.CaseInsensitiveRead`) is stage 28's concern (Utils/Json dispersal).

Two static fields gone in one stage; one of them goes via instance, the other via deletion. Both close Rule C for `Default.cs`.

## Scope

**Included:**
- Convert `_transportInOptions` from `private static readonly` to `private readonly` instance field (initialised at construction). The 3 read sites (lines 579, 628, 903) become `_transportInOptions` reads on `this` — no syntactic change, just the storage moves.
- Delete the `_jsonOptions` alias (line 46). Update the 2 read sites (lines 397, 527) to use `App.Utils.Json.CaseInsensitiveRead` directly. Line 646 already does this — the file becomes consistent.
- Sweep `grep -n "private static readonly" PLang/App/modules/http/code/Default.cs` — confirm zero hits after.

**Excluded:**
- Any change to the underlying `App.Utils.Json.CaseInsensitiveRead` static — that's stage 28 (Utils/Json disperses to consumers).
- Any change to `Filters.Transport.ForInbound` or any other `Filters/` machinery.
- Any HTTP behaviour change. The wire format, headers, retries, signing, streaming — all stay exactly as today.
- Splitting Default into smaller types or renaming. Default-as-`@this` for the HTTP code is settled.

## Deliverables

### `_transportInOptions` — static → instance

`PLang/App/modules/http/code/Default.cs:48-57` — today:

```csharp
private static readonly JsonSerializerOptions _transportInOptions = new()
{
    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    PropertyNameCaseInsensitive = true,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    TypeInfoResolver = new DefaultJsonTypeInfoResolver
    {
        Modifiers = { global::App.Channels.Serializers.Filters.Transport.ForInbound }
    }
};
```

After: drop `static`, keep everything else identical:

```csharp
private readonly JsonSerializerOptions _transportInOptions = new()
{
    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    PropertyNameCaseInsensitive = true,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    TypeInfoResolver = new DefaultJsonTypeInfoResolver
    {
        Modifiers = { global::App.Channels.Serializers.Filters.Transport.ForInbound }
    }
};
```

The 3 read sites (lines 579, 628, 903) already use `_transportInOptions` unqualified, which now resolves to `this._transportInOptions` — no source change needed there.

### `_jsonOptions` — delete the alias

`PLang/App/modules/http/code/Default.cs:46` — today:

```csharp
private static readonly JsonSerializerOptions _jsonOptions = App.Utils.Json.CaseInsensitiveRead;
```

After: line deleted entirely. The 2 read sites change:

```csharp
// Line 397 — today:
var signatureJson = JsonSerializer.Serialize(signResult.Signature, _jsonOptions);
// After:
var signatureJson = JsonSerializer.Serialize(signResult.Signature, App.Utils.Json.CaseInsensitiveRead);

// Line 527 — today:
parsed = JsonSerializer.Deserialize<object>(json, _jsonOptions);
// After:
parsed = JsonSerializer.Deserialize<object>(json, App.Utils.Json.CaseInsensitiveRead);
```

Line 646 already reads `App.Utils.Json.CaseInsensitiveRead);` directly — after the edit, all three sites in this file follow the same pattern. Stage 28 will handle the `App.Utils.Json` dispersal across the whole codebase.

### Caller propagation

External callers of either static: zero. Both fields are `private` — nothing outside the file reaches them. Sweep the file to confirm:

```bash
grep -n "_jsonOptions\|_transportInOptions" PLang/App/modules/http/code/Default.cs
```

Should show: 1 declaration of `_transportInOptions` + 3 reads (lines 579, 628, 903), zero `_jsonOptions` (after deletion).

External tests of `Default`: route through `IHttp` and the configured options are exercised by integration tests — none read the options directly.

### Definition of done

- `dotnet build PlangConsole` clean.
- `dotnet run --project PLang.Tests` green (baseline 2752/2752).
- `cd Tests && ../PlangConsole/bin/Debug/net10.0/plang --tester` green from a fresh rebuild.
- `grep -n "private static readonly" PLang/App/modules/http/code/Default.cs` — zero hits.
- `grep -n "_jsonOptions" PLang/App/modules/http/code/Default.cs` — zero hits.
- `grep -n "_transportInOptions" PLang/App/modules/http/code/Default.cs` — 4 hits (1 declaration + 3 reads).
- `grep -cn "App.Utils.Json.CaseInsensitiveRead" PLang/App/modules/http/code/Default.cs` — 3 hits (the consolidation onto a single reference pattern).

**Dependencies:** None. Independent of every other Tier 5 stage. Note that stage 28 will clean up `App.Utils.Json.CaseInsensitiveRead` itself; this stage just consolidates how Default reaches it.

## Design

### The smell this closes

Rule C — two static `JsonSerializerOptions` fields with no owner. They live for the process lifetime, not the App lifetime. They have no `@this` they belong to.

Where stage 24's eviction needed a shared owner (two callbacks reading the same options → introduced `Callback.Wire.@this` for navigability), stage 25's eviction is *local*. `_transportInOptions` is read only by Default's own methods. It doesn't need a navigable home — it just needs to belong to Default. Instance field is the correct OBP shape: the data lives on the type that owns it, not as process-global state.

### Why instance field, not a `Default/Transport/this.cs` subfolder

Stage 24's brief introduced a new subfolder (`Callback/Wire/`) because two callers (Ask + Error) needed a shared, navigable owner. Without that, a flat `WireOptions` property on `Callback.@this` would have been a Rule A violation. Stage 25 has only one caller (Default itself); the options are private to Default's implementation. There's no navigation chain needed, no public API surface, no Rule A risk. Instance field is the minimal correct shape. Don't add structure for the sake of symmetry.

### Why delete `_jsonOptions` instead of evicting it

The field's value is a reference-copy of an external static. It adds no configuration. Three behaviours match this reading:

1. Line 46 (declaration) reads as `_jsonOptions = CaseInsensitiveRead` — no `with { ... }`, no override, no transformation.
2. Line 646 in the same file already calls `App.Utils.Json.CaseInsensitiveRead` directly, bypassing the alias — so the codebase already treats the alias as optional.
3. Two of the three read sites (397, 527) use `_jsonOptions`; one (646) uses the long form. Asymmetry that exists today.

After deletion, all three sites use the long form consistently. Once stage 28 lands and `Utils.Json.CaseInsensitiveRead` either moves or splits, every reference in `Default.cs` updates as one — no special handling for an alias.

The plan one-liner said "→ instance fields" because that's the most common Rule C eviction. When the static is a *degenerate alias*, deletion is the equivalent fix: same Rule C closed, less code.

### Why not pull `_transportInOptions` to a sibling @this

Possible alternatives I considered and rejected:

- **`Http/Transport/this.cs` subfolder.** Would mount as `app.Code.Http.Transport.Options` (or similar). Too much shape for a private implementation detail. The options aren't navigated from outside; they don't need a navigation chain.
- **Stash on `Channels.Serializers.Filters.Transport`.** That class is the *modifier*, not the options. Owning the options there would conflate "what the filter does" with "how this one consumer configures itself."
- **Hand to a hypothetical `app.Code.Http.@this` collection.** Same issue as the subfolder — the options aren't shared across multiple HTTP impls; they're Default's own. The Code collection holds IHttp instances; it doesn't own each instance's private serialization config.

Instance field on Default is the minimum correct ownership. Promote later if a second consumer emerges.

## Risk + dependencies

**Risk: low.** Behaviour-preserving move + alias deletion. The compiler catches every miss; no test code reaches into either field directly.

Possible failure modes:
1. **A subclass of Default.** If anything inherits from Default and accesses `_transportInOptions`, the static-to-instance change still works (private fields don't affect subclasses anyway). `_jsonOptions` deletion would break a subclass that read the alias — sweep confirms there are no subclasses.
2. **Test parallelism.** Multiple Default instances now allocate separate options bags. Allocation cost is negligible (the modifier is a delegate ref); no functional concern.

**Dependencies: none.** Independent of stages 23, 24, 26, 27, 28. The downstream `App.Utils.Json.CaseInsensitiveRead` is stage 28's concern — no cross-talk.

## Watch for (coder eyes-on)

- **The `using` statements at the top of Default.cs.** After deletion of `_jsonOptions`, no using becomes dead — `JsonSerializer` is still used elsewhere; `Json.CaseInsensitiveRead` is still referenced via long-form. The four `System.Text.Json.*` usings stay.
- **The `_transportInOptions` declaration order.** Already in the right place after the constructors. Field-init runs at construction; no ordering change.
- **`_handler`'s nullability.** Default has both a parameterless ctor and a `Default(HttpMessageHandler handler)` ctor. The `_handler` field is `private readonly HttpMessageHandler?` — unchanged by this stage. Don't conflate the static-eviction with the constructor design.
- **Snapshot of `private static readonly` count before/after.** Before: 2. After: 0. If the count reads anything other than 0 after, sweep — there may be a third static somewhere not surfaced in the brief.

## Out of scope

- Any change to the HTTP behaviour: methods, retries, signing flow, streaming, error handling, headers.
- Any change to the `IHttp` interface or to other implementations of it.
- Anything in `App.Utils.Json` itself — that's stage 28.
- Renaming `Default.cs` to anything else — the `Default` shape is settled for the `code/` family in stage 19.

## Commit plan

```
runtime2-cleanup stage 25: DefaultHttpProvider statics → instance + alias deletion

Two static JsonSerializerOptions fields removed from Default.cs in two
different ways:

- _transportInOptions (full local options block, Filters.Transport.ForInbound)
  → instance field on Default. 3 read sites unchanged syntactically;
  storage moves from process-global to per-Default-instance.
- _jsonOptions (degenerate alias for App.Utils.Json.CaseInsensitiveRead)
  → deleted. The 2 read sites switch to the long form, matching what
  line 646 already did. File becomes consistent on the CaseInsensitiveRead
  reference pattern; stage 28 (Utils/Json disperse) will clean up the
  underlying static.

After: zero `private static readonly` fields in Default.cs. Rule C closed
for this file.

Files touched:
- PLang/App/modules/http/code/Default.cs — both statics gone, 5 read
  sites adjusted (3 unchanged for transport, 2 expanded to long-form
  for jsonOptions).

C# 2752/2752 + PLang 199/199 baseline preserved.

Tier 5 stage 25. Independent of all other Tier 5 stages.
```
