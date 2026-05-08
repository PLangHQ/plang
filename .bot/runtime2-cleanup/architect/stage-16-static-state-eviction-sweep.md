# Stage 16: `static-state-eviction-sweep`

**Read first:**
- `plan/principles.md` — **Rule C** (static fields are a missing `@this`).
- `plan/scope-map.md` — each evicted static lands on a shared (App-level) owner per the targets below.

**Goal:** Move every `static` field (including `static readonly`) into the `@this` that should own it. Most are mechanical; a few require a per-target design note. Static *methods* and `const` stay (per Rule C exceptions).

**Scope:**
- *Included:* the eight static-field hits identified by the Quick Screen in principles.md, each with its target listed below.
- *Excluded:* static methods (factory methods, conversion operators), `const` (compile-time constants), `AsyncLocal<T>` fields (correctly flow-scoped per Rule C exception #2). Static *lock objects* whose data is itself static — none on this codebase per the principles file.

**Deliverables:**

### Per-static migrations

| Today | Today's location | New owner | Mechanism |
|-------|------------------|-----------|-----------|
| `private static readonly JsonSerializerOptions _envelopeJsonOptions` | `Data/this.Envelope.cs:23` | private instance field on `Data.@this` | already inline-shaped; convert to instance |
| `private static bool _initialized` + `private static readonly object _initLock = new()` | `Utils/PlangTypeIndex.cs:26-27` | absorbed into `Types.@this` (partial class) per the plan | move file content to `App/Types/Registry.cs` (partial); statics become instance state on `Types.@this` |
| `private static readonly object _clrTypeFullNamesLock` | `Utils/PlangTypeIndex.cs:38` | same as above | absorbed into `Types.@this` |
| `private static readonly JsonSerializerOptions _options` | `Channels/Serializers/Serializer/PlangDataSerializer.cs:23` (or `Plang/Data.cs` post-stage-15) | private instance field on the serializer class | "JsonSerializerOptions disperse to consumers" pattern (already followed elsewhere) — make per-instance |
| `private static readonly JsonSerializerOptions _jsonOptions = App.Utils.Json.CaseInsensitiveRead` | `modules/http/providers/DefaultHttpProvider.cs:46` | private instance field on `DefaultHttpProvider` | per-instance |
| `private static readonly JsonSerializerOptions _transportInOptions = new()` | `modules/http/providers/DefaultHttpProvider.cs:48` | private instance field | per-instance |
| `private static readonly Stopwatch _buildTimer = new()` | `modules/builder/providers/DefaultBuilderProvider.cs:18` | private instance field on the provider | per-instance — each builder owns its own timer |
| `private static readonly JsonSerializerOptions _options` | `Callback/AskCallback.cs:106` | private instance field on `AskCallback` | per-instance |
| `private static int _requestCount` | `modules/llm/providers/OpenAiProvider.cs:41` | **DELETED** (per Ingi 2026-05-07; todo logged in Documentation/Runtime2/todos.md) | also delete the increment-and-throw block + the `5000` cap const |
| `private static readonly object _gate = new()` + `private static Dictionary<System.Type, MethodInfo>? _registry` | `Choices/this.cs:18` | private instance fields on `Choices.@this` | the registry is currently a process-wide cache; per-instance is correct since Choices.@this is one-per-app |

### Plus: `App/Variables/Reserved.cs` (already settled)

Per the plan one-liner, `Utils/ReservedKeywords.cs` was supposed to move to `App/Variables/Reserved.cs` (all const/readonly). Verify on read:

- `App/Utils/ReservedKeywords.cs` exists today — move its content (the well-known variable names like `!Test`, `!Signature`, etc.) to `App/Variables/Reserved.cs`.
- All values become `public const string` or `public static readonly` (per the const/readonly rule — these are values, not state, and `const` is allowed).
- `Utils/ReservedKeywords.cs` deletes; callers sweep to `App.Variables.Reserved.X`.

### File-level changes summary

| File | Change |
|------|--------|
| `App/Data/this.Envelope.cs` | static → instance field |
| `App/Utils/PlangTypeIndex.cs` | DELETE (content absorbed into `App/Types/Registry.cs` partial) |
| `App/Types/Registry.cs` (NEW partial of Types.@this) | gains the type-name index logic, instance fields |
| `App/Channels/Serializers/Serializer/Plang/Data.cs` (post-stage-15 path) | static → instance |
| `App/modules/http/providers/DefaultHttpProvider.cs` | 2 statics → 2 instance |
| `App/modules/builder/providers/DefaultBuilderProvider.cs` | 1 static → instance |
| `App/Callback/AskCallback.cs` | 1 static → instance |
| `App/modules/llm/providers/OpenAiProvider.cs` | DELETE field + increment block + cap const |
| `App/Choices/this.cs` | 2 statics → 2 instance |
| `App/Utils/ReservedKeywords.cs` | DELETE (content moved) |
| `App/Variables/Reserved.cs` (NEW) | const/readonly values from ReservedKeywords |

### Caller sweeps

After migration:
- `grep -rn "ReservedKeywords\." PLang/ PLang.Tests/ --include='*.cs'` — zero hits; replaced with `Reserved.X`.
- `grep -rn "PlangTypeIndex" PLang/ PLang.Tests/ --include='*.cs'` — zero hits; replaced with `app.Types.X`.
- `grep -rn "_requestCount\|MaxRequests\|RequestLimitException" PLang/App/modules/llm/ --include='*.cs'` — zero hits.
- General Rule C grep (per principles): `grep -rE "^\s+(public|private|internal|protected)\s+static\s+" PLang/App --include='*.cs'` — only static *methods*, factory helpers, `const`, and `AsyncLocal<>` should remain.

### Definition of done

- `dotnet build PlangConsole` clean.
- `dotnet run --project PLang.Tests` green (baseline 2752/2752).
- `cd Tests && ../PlangConsole/bin/Debug/net10.0/plang --tester` green from a fresh rebuild.
- Greps above return the expected zero-hit results.
- The OpenAi todos.md entry can be marked RESOLVED.

**Dependencies:** None on stage 15 specifically. Independent. Stage 15 may rename some of the files referenced above (`PlangDataSerializer.cs` → `Plang/Data.cs`); coder can choose order — either land 15 first then 16 references new paths, or land 16 first against today's paths.

## Design

### The smell this closes

Rule C — static fields are a missing `@this`. Each static field above lives outside any class instance's lifecycle. Per-process state instead of per-app state means:

- Tests get cross-test pollution (a static counter from test 1 is still incrementing in test 2).
- Multi-app processes (rare today, but a future scenario) share state they shouldn't.
- The eviction makes each piece of state owned by an explicit `@this` whose lifecycle is well-defined.

### The OpenAi.requestCount special case

Per Ingi 2026-05-07 the per-process counter on OpenAiProvider is the wrong shape. It's a temp blocker that prevents runaway LLM requests during dev — but it's per-process (not per-actor, not per-app), and on a class that's per-actor-resolved. Wrong scope; wrong place. Delete entirely. No replacement scope today; if rate-limiting becomes a real need, design properly when the requirement is clear.

### Files touched

~10 files modified, 2 deleted (`PlangTypeIndex.cs`, `ReservedKeywords.cs`), 2 created (`Types/Registry.cs` partial, `Variables/Reserved.cs`).

### Risk + dependencies

**Risk: medium.** Mechanical migrations but spread across many files. Build catches type/access misses.

Possible failure modes:
1. **Boot-order dependencies on PlangTypeIndex's lazy init.** Today's `_initialized` guard ensures one-time init across the process. After the move to `Types.@this`, init happens per-Types-instance — which is per-app (Types is App-level shared per scope-map). Still one-time per app, just no longer process-wide.
2. **The 9-month-old `5000` request cap on OpenAi** — its removal is intentional. If someone genuinely needs rate-limiting, they'll notice and ask.
3. **The `_options` JsonSerializerOptions on serializers** — making them per-instance means each Serializer instance allocates its own options. Slightly more memory; the principle is that options are configuration owned by the serializer.
4. **Choices' `_gate + _registry` static** — the registry caches reflection results across the process. Moving to per-instance means each Choices.@this rebuilds its registry. Fine since Choices is one-per-app.

**Dependencies: none.**

### Tests

**No new tests required.** Behavior preserved (except the OpenAi cap removal — observable only when someone hits 5000 LLM calls, which no test does).

**Existing test coverage to verify:**
- `PLang.Tests/App/Types/EngineTypesTests.cs` — verifies the Type registry post-move.
- Tests for HTTP, LLM, Builder, Callback subsystems — verify per-instance JsonSerializerOptions still work.
- `Tests/` — full PLang suite.

### Watch for (coder eyes-on)

- **The PlangTypeIndex eviction** — non-trivial. `_initialized` and `_clrTypeFullNamesLock` are part of a one-time init pattern. Check the existing `Types.@this` for whether it already has init logic; if so, integrate; if not, add a ctor-time init.
- **Choices' static methods that read `_registry`** — those become instance methods using `_registry` field. Public surface might change accessibility; verify callers.
- **The OpenAi block to delete** — read line ~40 + the increment-and-throw block (probably in the request flow). Delete the field, the increment, the throw, and the cap const.
- **JsonSerializerOptions per-instance memory cost** — each provider/serializer instance now allocates its own options. That's the cost of per-instance scoping; accept it. (Some places use `App.Utils.Json.CaseInsensitiveRead` as a shared static — those stay until the eviction reaches them; out of scope here.)
- **Reserved keywords' const-vs-readonly choice** — strings can be `const`. Use `const string` everywhere; `static readonly` is fine for any non-string (none expected).

### Stages that follow this one

- **Stage 15** (`compound-name-rename`) — same Tier 4 batch; either order works. If 15 lands first, paths in stage 16 update accordingly.
- **Stage 19** (`provider-to-code-rename`) — biggest sweep; own session.

### Out of scope

- Static *methods* (factory methods, helpers) — stay per Rule C.
- `const` — stays.
- `AsyncLocal<T>` fields — stay (correct flow-scoping).

## Commit plan

```
runtime2-cleanup stage 16: static-state eviction sweep (Rule C)

Per principles.md Rule C: static fields are a missing @this. Eight
hits in App/ today; each gets the right owner.

Per-instance migrations (mostly mechanical):
  Data/this.Envelope.cs  _envelopeJsonOptions → instance
  Channels/Serializers/Serializer/Plang/Data.cs  _options → instance
  modules/http/providers/DefaultHttpProvider.cs  _jsonOptions,
                                                  _transportInOptions → instance
  modules/builder/providers/DefaultBuilderProvider.cs  _buildTimer → instance
  Callback/AskCallback.cs  _options → instance
  Choices/this.cs  _gate, _registry → instance

Relocations:
  Utils/PlangTypeIndex.cs  → Types/Registry.cs (partial of Types.@this)
                             — _initialized, _clrTypeFullNamesLock,
                               _initLock all become instance state
  Utils/ReservedKeywords.cs  → Variables/Reserved.cs
                              — values become const string

Deletions:
  modules/llm/providers/OpenAiProvider.cs:41  _requestCount,
                                                cap const, increment-
                                                and-throw block
                                              (per Ingi 2026-05-07;
                                               todos.md entry resolved)

Build catches caller misses; greps verify zero residuals on the
old type names.

Static *methods*, const, and AsyncLocal<T> stay (Rule C exceptions).
```
