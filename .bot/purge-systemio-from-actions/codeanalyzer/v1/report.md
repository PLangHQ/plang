# codeanalyzer v1 — purge-systemio-from-actions

**Branch:** `purge-systemio-from-actions`
**Diff base:** `9fb2cca44^` (architect handoff parent)
**Scope:** 103 files, +4485/-669
**Verdict:** **PASS (NEEDS WORK — low only)**

## Method

Five-pass analysis per character file. The diff is large but structurally tight: the System.IO purge ripples through `path.@this` (new verbs, JsonConverter), goals (`Path`/`PrPath` flipped from `string` to `path?`), AppGoals (path-keyed dicts), ring-2 handlers (test/settings/llm/module/code/debug/ui/http/builder), and the source generator (PLNG002 at error severity). Three Explore agents covered:

- **A** — Pass 1 (OBP) + Pass 5 (deletion) on the `app.types.path.**` core + Conversion + serializers.
- **B** — Pass 1-3 + Pass 5 on the ring-2 handler sweep (18 files).
- **C** — Pass 4 (behavioral / cross-file) on the typing flips (Goal.Path, JsonConverter, AppGoals, Error/CallChain, PLNG002).

Each agent produced ≤8 candidates with named failure scenarios. Verification step (manual read of the cited lines) refuted the high-severity candidates as documented design or Roslyn-handled. Surviving findings are all LOW.

## Verified clean

- **System.IO purge is real.** No disk-touching `File.*` / `Directory.*` / `FileInfo` / `Path.GetFullPath` survives outside the documented exempts (`app.types.path.**`, `PLang.Generators`, `MarkdownTeaching`). The PLNG002 analyzer enforces this at **error** severity (`PLang.Generators/Diagnostics/Plng002.cs:147`) and a clean `dotnet build PlangConsole` confirms zero PLNG002 diagnostics. Remaining `System.IO` mentions in modules are either comments documenting removal, `System.IO.IOException` / `FileNotFoundException` as throw types, or `MemoryStream` (in-memory, not disk) — none touch the filesystem.
- **D13 discipline (`.Absolute` + `Authorize` first).** Every `.Absolute` reach outside `app.types.path.**` is preceded by `await path.Authorize(verb)` with a `.Success` check. Audited: `settings/Sqlite.cs:30-58` (D9b), `code/this.Snapshot.cs:97-110`, `code/load.cs:27-34`, `module/add.cs:19-25`, `debug/this.cs:403,458-460`, `ui/code/Fluid.cs:329`, `http/code/Default.cs:1049-1093`, `builder/code/Default.cs:153`.
- **Execute verb deployed correctly.** Every `Assembly.LoadFrom`-equivalent reach uses the new `Execute` permission (Unix r/w/x) instead of `Read`. Audited: `code/load.cs:27-28`, `code/this.Snapshot.cs:97`, `module/add.cs:25`.
- **AppGoals dual indexing is internally consistent.** `_goals` (PrPath-keyed) + `_byPath` (Path-keyed) + `_byName` (Name-keyed, case-insensitive) — `Add` writes all three, `Remove` clears all three, `Clear` clears all three. Path's own `Equals`/`GetHashCode` uses `RootComparison` so the dict keying matches `Relative`/`IsUnder` semantics (no separate `StringComparer` needed).
- **Stub-path-on-null-context is documented contract.** `path/this.JsonConverter.cs:13-17,40-44` and `path/this.cs:196-203` explicitly call out the implicit `string→path` lift as "test fixtures and JSON deserialize without scope; Authorize callers will explode on this Path — that's the contract." Conversion.cs:139-141 routes through `ContextualReadOptions(context)` whenever context is available.
- **PLNG002 alias robustness.** Roslyn resolves `using Data = app.data.@this;` at the symbol level, so `INamedTypeSymbol.Name == "@this"` regardless of how the property type is spelled. The check on `Plng002.cs:189-191` (`nt.Name != "@this" && nt.Name != "this"` + ns check) catches aliased forms.
- **CallChainRenderer Path-equivalence.** The `string.Equals(Ordinal)` → `Equals(path, path)` switch uses Path's `RootComparison` (OrdinalIgnoreCase on Windows, Ordinal on Linux) — this is the **correct** semantics for "did the same goal run twice?" (filesystem identity, not byte identity). Not a regression.

## Findings (LOW severity only)

### N1 — `Json.cs:26-28` dead allocation when `options` is non-null (Pass 5)

```csharp
private Json(JsonSerializerOptions? options, actor.context.@this? context)
{
    var pathConverter = context != null
        ? new global::app.types.path.JsonConverter(context)
        : new global::app.types.path.JsonConverter();
    _options = options ?? new JsonSerializerOptions { ... Converters = { ..., pathConverter } };
}
```

When `options` is non-null (the `ForView` / `WithIndentation` path, lines 50-63, 121-128), `pathConverter` is allocated but never referenced — it only appears inside the right-hand side of `??`, which is only evaluated when `options == null`. The context-bound converter survives into `viewOptions` via `JsonSerializerOptions`'s copy constructor (which copies the `Converters` collection from the parent), so behavior is correct. The allocation is just wasted work and obscures the constructor's intent.

**Deletion test:** Move the `pathConverter` allocation inside the `??` branch's initializer (use a local at the start of the initializer or a static helper). Compilation passes; `ForView` still inherits the parent's converter; ChannelsSerializerForViewPathConverterTests still pass. If they don't, the inheritance assumption is wrong and *that* is the real bug — but the test green-band (3025/3025 C# + 210/210 PLang) suggests inheritance is fine.

**Fix:** trivial; leave for coder.

### N2 — `Conversion.cs:51-64,142` per-call `JsonSerializerOptions` allocation (Pass 2)

```csharp
var readOpts = context != null ? ContextualReadOptions(context) : _caseInsensitiveRead;
```

`ContextualReadOptions(context)` allocates a fresh `JsonSerializerOptions` (with 4 converters) on every `TryConvertTo` call where context is non-null. `TryConvertTo` runs on every `Data<T>.As<T>` resolution, every action property hydration, every variable type-coercion — hot paths. A per-Context cache (e.g., `Lazy<JsonSerializerOptions>` keyed on the Context identity, stored on `actor.context.@this`) would eliminate this. The Context itself already lives for the actor's lifetime, so a per-context options bag has no eviction problem.

**Why not in this branch:** the architect deliberately introduced `ContextualReadOptions` as a one-shot in stage 3 to avoid AsyncLocal scope; the caching layer was a deferred decision. Note in `Documentation/Runtime2/todos.md` if not already there.

### N3 — `path/this.cs:204-205` implicit `string → path` always builds file-scheme stub (Pass 4)

```csharp
public static implicit operator @this(string raw)
    => new file.@this(raw) { Raw = raw };
```

The comment explicitly justifies this for test fixtures and JSON deserialize boundaries without Context, and `Authorize` is documented to explode on stub paths. But this operator fires *anywhere* a `string` flows into a `path` parameter — including production code where Context *is* in scope but the call site happens to hand a `string` (e.g., a helper that took a `string` overload). The conversion silently bypasses `path.Resolve(raw, ctx)` and the scheme registry.

Verified safety in current diff: every action-handler property in scope is `Data<path>` (PLNG002 enforces no `Data<string>` Path slots), and every Conversion site threads Context. So the operator is **currently** only firing in the documented contexts. But it is a footgun: a future handler that accepts a `string` parameter for any reason would silently file-scheme it. The deletion test from agent A holds — if you delete this operator and recompile, every use site becomes visible and can be audited individually.

**Recommendation:** leave for now; revisit when convenient. If you delete it, replace `goal.PrPath = "/.build/x.pr"` style test-fixture writes with explicit `path.@this.Resolve("/.build/x.pr", ctx)` or a dedicated test helper.

### N4 — `AppGoals.Add` last-write-wins on `_byName` collisions (Pass 4, latent)

```csharp
if (!string.IsNullOrEmpty(goal.Name))
    _byName[goal.Name] = goal;
```

`_byName` is `OrdinalIgnoreCase`-keyed. Two goals with `Name="Start"` (separate files / folders) silently overwrite each other in `_byName`. The other two dicts (`_goals`, `_byPath`) keep both. `Get("Start")` returns whichever was added last; the first remains discoverable only via the `MatchesByForm` scan over `_byPath` (line 79-84). Subsequent `Remove("Start")` removes the last-written goal, leaving the first orphaned in `_byName` (never re-indexed). The orphan still works because the scan path still finds it — but the symptom on a duplicate-name build is "my first goal silently disappeared."

The builder rules forbid duplicate names per app, so this is a "the builder catches it elsewhere" situation — but Add() itself is silent about it. Consider an `ArgumentException` or a debug-channel warning when `_byName.ContainsKey(goal.Name)` at Add time.

**Severity:** LOW (depends on user mis-step that the builder should reject upstream).

### N5 — `AppGoals.TryLoadPr` writes user-provided `name` into `_byName` alongside `goal.Name` (Pass 4, latent)

```csharp
if (goal != null && !string.IsNullOrEmpty(name))
    _byName[name] = goal;  // line 181, also line 204
```

`LoadFromFileAsync` already calls `Add(goal)` internally (line 369), which writes `_byName[goal.Name] = goal`. Lines 181 and 204 then *additionally* write `_byName[name] = goal` where `name` is the user-provided lookup string (could be `"Foo"` while `goal.Name` is `"foo/bar"`). Result: two `_byName` entries point to the same goal. `Remove(goal.Name)` removes only the canonical entry; the alias entry survives and now points to a goal that's been removed from `_goals`/`_byPath` — a stale-cache hit on future `Get("Foo")`.

**Trigger:** `await goals.GetAsync("AnyName")` for a `.pr` not already cached, where `AnyName` doesn't match `goal.Name`. Then `goals.Remove(goal.Name)`. Then `goals.Get("AnyName")` returns the removed goal.

**Severity:** LOW. The "remove and re-get under a different alias" pattern is rare in the codebase; tests didn't exercise it. Fix is either to skip the extra `_byName[name] = goal` write (Add already did it) or to track the alias separately for clean Remove.

## What I'm not flagging

- The Phase 1 candidates from agents that turned out to be design choices: stub paths from null context (documented contract), implicit `path → string?` operator (justified for null-aware assertions and test interop), CallChainRenderer's switch to platform-aware equality (correct for filesystem identity), PLNG002 alias resolution (Roslyn handles it), cycle-detection skip on null PrPath (pre-existing, not introduced here).
- `Path.Combine` allowlist bullets in `good_to_know.md:1100-1105`: the doc lists pure-name-math methods as allowlisted, but `Plng002.cs:147` only allows the four separator constants (`DirectorySeparatorChar`, `AltDirectorySeparatorChar`, `PathSeparator`, `VolumeSeparatorChar`) plus what's hard-coded in `AllowedSystemIoPathMembers`. Verified the test `Fires_OnSystemIoPathCombine_UnderModulesNamespace` confirms `Path.Combine` *does* fire — i.e., the analyzer is **stricter** than the doc reads. Either tighten the doc or relax the analyzer; not a bug, but a doc/code mismatch worth a one-line fix in good_to_know.md.

## Verdict

**PASS (NEEDS WORK — low only).** The branch achieves its stated goal — System.IO is gone from action handlers and engine code, replaced by a typed `path.@this` surface that auth-gates every disk touch. PLNG002 at error severity locks the door. Five low-severity findings (3 simplification, 2 latent correctness) are worth tracking but none block merge.

Next: tester.

```
VERDICT: PASS
Issues (low, optional follow-up):
  N1 Json.cs unused pathConverter alloc when options is provided
  N2 ContextualReadOptions per-call allocation (caching opportunity)
  N3 implicit string→path operator is a future footgun
  N4 AppGoals.Add silent name-collision overwrite
  N5 AppGoals.TryLoadPr writes user-name aliases into _byName (stale-cache risk on Remove)
Next: run.ps1 tester purge-systemio-from-actions "Review the code on branch purge-systemio-from-actions" -b purge-systemio-from-actions
```
