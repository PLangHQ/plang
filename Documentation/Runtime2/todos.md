# Runtime2 Todos

## AllowedValues attribute for builder-time parameter constraints
**Date:** 2026-03-26
**Context:** Crypto module `Algorithm` param is a free-form string. Builder LLM guesses values. Want to list valid algorithms (from ICryptoProvider) at build time so the LLM picks correctly.

**Approach:** Add `[AllowedValues("keccak256", "sha256")]` attribute on action parameters. Source generator or builder prompt reads it and injects `allowedValues` into the parameter schema the LLM sees. Static for now; when v2 builder arrives, could dynamically query providers instead.

**Applies to:** Any action parameter with a known set of valid values (crypto algorithms, encoding names, etc.)

---

## PLang Linter / Static Analyzer
**Date:** 2026-02-13
**Context:** During Engine refactoring (folding AppContext into Engine, renaming IO→Channels). Thinking about how to make dependencies discoverable at the goal/action level.

**Idea:** Before build, scan all .cs handler files and analyze methods:
- If a method body references `engine` or `context`, auto-add `[Engine]`, `[Context]` attributes to the method
- This enables runtime queries like `engine.Goals["Engine"]` — get all goals that use the engine
- More dependency tags likely exist beyond Engine/Context (FileSystem? Channels? MemoryStack?)

**Open questions:**
- What granularity? Per-goal, per-step, per-action?
- What other dependencies to track?
- Is this build-time analysis or a separate linter pass?

---

## External Library Integration (engine.astro) — Partially Done (2026-02-14)
**Date:** 2026-02-13
**Context:** During OBP naming work on Lifecycle/Bindings.
**Status:** `library.load` handler implemented — PLang can load external DLLs at runtime. Remaining: builder auto-detection of unknown actions, calling external library actions from PLang steps.

**Idea:** `engine.Libraries` — register and call external .dll libraries from PLang.
- ~~`engine.Libraries.Add('my.dll')` — user registers their dll~~ ✅ Done via `library.load` handler
- PLang syntax: `astrolib.dll, getCalculation(x, y), write to %result%`
- Two paths: user writes it in PLang, OR the builder compiles C# code and auto-adds `Add('01. change_name.dll')` to the build output
- Libraries contain actions — once registered, their methods become callable from PLang steps

---

## PLang as Embeddable Engine / Platform
**Date:** 2026-02-13
**Context:** Thinking about the big picture — PLang's Engine as a platform.

**Core idea:** The engine is a self-contained root object. Everything hangs off it. You don't configure it, you use it. PLang isn't a programming language — it's an engine. The language is just how you talk to it.

### Design ideas

**Domain data on the engine:** `engine.Products = %products%` — the key-value store is already there. Domain objects live alongside cache, IO, events, file system. The engine is both infrastructure AND data host.

**Semantic querying for free:** `%engine.Products% that fits with %query%` — not a new feature. The builder sends natural language steps to the LLM. Products are in memory, query is a variable, LLM resolves it. Cache already exists for results.

**Engine-level properties as conventions:** `engine.Summary = call goal GetSummary %product%` — attaching behavior to the engine, not just data. Goals become callable properties. The engine becomes a domain-specific runtime.

**Self-hosting:** PLang code can manipulate its own engine — add events, load goals, run goals. PLang embedding PLang. One engine orchestrating others.

---

## ~~Libraries Replaces ActionRegistry~~ ✅ DONE (2026-02-14)
**Date:** 2026-02-13
**Context:** Discussing engine as platform — making external DLLs first-class.
**Completed:** ActionRegistry fully replaced by Library + Libraries. External DLL loading implemented via `library.load` handler. Documentation updated.

- Built-in handlers (variable, file, output, etc.) are Library[0] — everything uniform
- `engine.Libraries.Add(library)` — adds external library
- Handler resolution walks the list: `engine.Libraries.GetCodeGenerated("variable", "set", context)`
- From PLang: `use library 'mylib.dll'` → `library.load` handler
- Library is simple: Name + Assembly + discovered actions
- ActionRegistry functionality absorbed into Libraries

---

## Engine Goal Properties (GoalCall as Value)
**Date:** 2026-02-13
**Context:** Engine key-value store already exists. Values can be any type.

**Idea:** The key-value store value can be `GoalCall`. When you set `engine["Summary"] = GoalCall(...)`, accessing it runs the goal. This turns engine properties into callable behavior — navigation evaluates it lazily.

```
engine.Summary = call goal GetSummary %product%
```

No new mechanism needed — the key-value store already accepts `object?`, and `GoalCall` is already a type. The resolution layer just needs to recognize GoalCall values and execute them on access.

---

---

## "Website in 10 Lines" — Language Design Exercise
**Date:** 2026-02-13
**Context:** Marketing says lead with examples. Write the .goal file first — it becomes the spec for the HTTP module.

**Idea:** Write the actual .goal file for a website in ~10 lines. Even though the HTTP module isn't in Runtime2 yet, the goal file defines what the language SHOULD look like. Design the language, the runtime follows. This becomes the reference spec for:
- `http.listen` / `http.route` / `http.respond` handlers
- Template rendering integration
- How routes map to goals
- What a minimal PLang web app looks like

Once the .goal file reads right, build the module to match it.

---

### Documentation / marketing framing

Lead with examples, not explanations:
- "A website in 10 lines" — Start, routes, render. No framework, no config.
- "You didn't set anything up" — `cache.set`, `cache.get` — it just works. No DI, no wiring.
- "Search in plain English" — `%engine.Products% that fits with %query%` — not SQL, not an API, just a question.
- "A CMS in 20 lines" — webserver + database + cache + templates.

Target audiences:
- Non-programmers: "You can read it, so you can write it"
- Developers: "Embeddable runtime with batteries included — cache, IO, events, LLM, file system, serialization. Add your domain logic."
- Businesses: "Your entire backend in files you can read in 5 minutes"

---

## OBP Fixes to Libraries API (Changes 2-4)
**Date:** 2026-02-15
**Context:** During OBP refactoring — Change 1 (extract `engine.Property`) is done. Three remaining violations in Libraries.

**Changes:**
1. **Remove ICodeGenerated requirement from `Library.Discover`** — external DLLs won't have the source generator. Add `ReflectionAdapter` (private nested class in Library) that wraps any `[Action]` type with `Run()` method, mapping parameters via reflection + `TypeMapping.ConvertTo`.
2. **Remove namespace filter from `Discover`** — module name = last namespace segment automatically. Remove `Namespace` parameter from `library/load.cs` too.
3. **Remove `PLangContext` from `Libraries.GetCodeGenerated` signature** — context only used for error creation, use context-free `ActionError` overloads instead.

**Plan file:** `/home/claude/.claude/plans/sequential-roaming-dragon.md` has the full plan with file list and test changes.

---

## Foundation Checklist — Before Mass Action Production
**Date:** 2026-02-22
**Context:** Engine graph audit. These must be resolved before we can start cranking out actions assembly-line style.

| # | Item | Status | Notes |
|---|------|--------|-------|
| 1 | **system.sqlite** | ✅ DONE | `SqliteDataSource.cs` + `IDataSource.cs`. Per-actor `.db/{name}.sqlite`, in-memory for testing/building. |
| 2 | **Setup.goal** | ✅ DONE | `Setup/this.cs` — discovers by convention, runs once per step, persists in system DataSource. Integrated into `Executor.cs` startup. |
| 3 | **Settings** | ✅ DONE | Merged 2026-02-22. Scope chain, `ISettings`, source-generated props, `SettingsData` bridge, settings module handlers. |
| 4 | **Pluggable action implementations** | ✅ DONE | `Engine.Providers` — type-keyed service registry. Modules define provider interfaces (e.g., `ICryptoProvider`), register defaults, PLang developers override via DLL. Implemented with crypto module (2026-03-19). |
| 5 | **Retry testing** | ✅ DONE | `Tests/Runtime2/ErrorRetryOnly/` (bare + timed retry) and `Tests/Runtime2/ErrorGoalFirst/` (GoalFirst order). |

---

## engine.Action<T> — First-Class Module Objects on Engine
**Date:** 2026-02-21
**Context:** During ISettings scaffolding. Settings currently lives at `engine.Settings.For<archive.Settings>(context)`. Works, but the long-term navigation should be `engine.Action<archive.@this>().Settings.Max` — each action module as a first-class object on the engine with capabilities hanging off it (settings, and later potentially config, health, metrics, etc.).

**Idea:** Introduce `engine.Action<T>()` where T is the module's `@this` class (e.g., `actions.archive.@this`). Returns a module-level aggregate object that carries per-module capabilities. Settings slots under it as the first capability.

**Why parked:** Right now settings is the only capability that would hang off it. One capability doesn't justify a new abstraction. Introduce `Action<T>` when a second capability needs a home — then settings moves under it, internals unchanged, just the navigation path changes.

**When to revisit:** When any of these come up — per-module config, module health checks, module-level events, or any other per-module concern beyond settings.

---

## Set Up PLang Test Environment for Bot Pipeline
**Date:** 2026-02-21
**Context:** Tester bot cannot run PLang .goal tests because (1) `plang` binary is not on PATH (PlangConsole not built), (2) `OPENAI_API_KEY` env var not set. Both needed for `plang p build && plang p !test`.

**Action items:**
- Build PlangConsole and add to PATH (or symlink)
- Set `OPENAI_API_KEY` in the environment so bots can run PLang tests
- Consider a config file or `.env` approach so the key persists across sessions

---

## Bot Reminder/Notification System
**Date:** 2026-02-21
**Context:** Currently the only way to capture "do this tomorrow" reminders is the todo list. Need a proper reminder mechanism so bots can flag time-sensitive items.

**Idea:** For now, todos serve as reminders. Longer term, consider a structured reminder system (date-tagged entries that surface at session start).

---

## ~~Safe cast in Settings.Resolve<T>~~ ✅ DONE (2026-02-21)
**Date:** 2026-02-21
**Context:** Code analyzer found `Settings.Resolve<T>` uses hard cast `(T)value`. Tester elevated to CRITICAL — C# unboxing + JSON deserialization = production crash.
**Completed:** Coder v2 replaced with `Cast<T>` helper: `is T` → `Convert.ChangeType` → fallback. Tests added for int→long widening and type mismatch fallback.

---

## ~~Cast<T> doesn't handle enums~~ ✅ DONE (2026-02-21)
**Date:** 2026-02-21
**Context:** Code analyzer v2 review of coder's `Cast<T>` fix.
**Completed:** Coder added `if (target.IsEnum) return (T)Enum.ToObject(target, value)` to `Cast<T>`. Test `Resolve_WidensIntToEnum` added.

---

## ~~Clone() loses SettingsScope~~ ✅ DONE (2026-02-21)
**Date:** 2026-02-21
**Context:** Code analyzer v2 review.
**Completed:** Coder added `SettingsScope = SettingsScope` to `PLangContext.Clone()` object initializer. Test `Clone_PreservesSettingsScope` added.

---

## GoalRunAsync settings test is simulation, not integration
**Date:** 2026-02-21
**Context:** Code analyzer v2 review. `GoalRunAsync_ScopesSettingsPerGoal` manually does `context.SettingsScope = null` / `context.SettingsScope = saved` to simulate RunAsync. If the real RunAsync changes its save/restore logic, this test still passes. Doesn't exercise the actual code path in `Goal/Methods.cs:29-32,89`.

**Fix:** Write an integration test that creates a Goal with Steps and actually calls `RunAsync`, then verifies settings isolation. Requires test infrastructure for minimal Goal construction.

---

## Verify Defaults scope is ConcurrentDictionary
**Date:** 2026-02-21
**Context:** Code analyzer v2 review. `engine.Settings.Defaults` is shared across all contexts. Thread safety is assumed via ConcurrentDictionary. Rather than testing concurrent behavior, verify the type choice is correct — assert it IS a ConcurrentDictionary so a future refactor to Dictionary would break a test.

---

## Investigate: Kind as a Rich Object (ConcurrentDictionary<string, Kind>)
**Date:** 2026-02-21
**Context:** Auditor review of data-envelope-architecture. `_allKinds` is a `HashSet<string>` protected by manual locking. No `ConcurrentHashSet` exists in .NET. Using `ConcurrentDictionary<string, byte>` as workaround loses `TryGetValue` returning the canonical key.

**Idea:** Instead of a dummy value, store a `Kind` object. `_allKinds` becomes `ConcurrentDictionary<string, Kind>`. This eliminates manual locking AND makes Kind powerful:
- `kind.ContentTypes` — list all MIME content types for this kind (e.g. `image` → `image/jpeg`, `image/png`, ...)
- `kind.Extensions` — list all extensions (e.g. `image` → `.jpg`, `.png`, ...)
- `kind.Compressible` — whether this kind is compressible
- Kind becomes a first-class domain object instead of a bare string

---

## ~~UnwrapJsonElement loses decimal precision~~ ✅ DONE (2026-02-21)
**Date:** 2026-02-21
**Context:** Code analyzer higher-level review of data-envelope-architecture.
**Completed:** Coder extracted `UnwrapJsonNumber`: `TryGetInt64 → TryGetDecimal → GetDouble`. Tests verify `19.99` stays `decimal` and `42` stays `long`.

---

## ~~MemoryStack.Clone() doesn't propagate Context~~ ✅ DONE (2026-02-21)
**Date:** 2026-02-21
**Context:** Code analyzer review.
**Completed:** Coder added `clone.Context = Context` to `MemoryStack.Clone()`. Existing test updated, new `Clone_PreservesContext` test added.

---

## ~~MemoryStack.Get returns error Data for depth-exceeded paths~~ ✅ DONE (2026-02-21)
**Date:** 2026-02-21
**Context:** Tester v7 Finding #2.
**Completed:** Coder added `Get_DeeplyNestedPath_ReturnsErrorData` test — 101+ dot path, asserts `Success == false` and `Error.Key == "NavigationDepthExceeded"`.

---

## JsonStringNavigator: Parse Once on First Access, Update Value + Type
**Date:** 2026-02-22
**Context:** Security finding #6 — JsonStringNavigator re-parses the full JSON string on every `.` navigation. Size limit (10MB) added, but the re-parse-every-time behavior remains.

**Problem:** `%response.name%` then `%response.email%` parses the same string twice. For a 5MB API response accessed 10 times, that's 50MB of unnecessary parsing.

**Fix for coder:**
1. On first navigation in `JsonStringNavigator.GetProperty`, parse the string via `UnwrapElement`
2. Replace `Data.Value` with the parsed result (either `Dictionary<string, object?>` for `{...}` or `List<object?>` for `[...]`)
3. Update `Data.Type` accordingly — JSON can be object OR array:
   - `{...}` → `dict` (or whatever PLang type is correct for `Dictionary<string, object?>`)
   - `[...]` → `list` (for `List<object?>`)
4. Subsequent navigations then go through `DictionaryNavigator` or `ListNavigator` (both higher priority than `JsonStringNavigator`) — no re-parsing

**Key insight:** JSON is not one type — `{object}` and `[array]` are different. The parsed Value type determines which navigator handles it. Don't assume dict.

**Note:** This changes what `Value` returns after first access (string → dict/list). Callers that expect the raw JSON string need to read Value *before* navigating, or we need to preserve the original string somewhere (e.g., a `RawValue` property). Check if any caller depends on `Value` being a string after navigation.

---

## ~~fromJson wraps depth-exceeded as "Invalid JSON"~~ ✅ DONE (2026-02-21)
**Date:** 2026-02-21
**Context:** Tester v7 Finding #3.
**Completed:** Coder catches `InvalidOperationException` separately with key `"JsonDepthExceeded"`. Test `FromJson_DeeplyNested_Fails` and `FromJson_DecimalNumber_PreservesPrecision` added.

---

## Builder Drops onError — 5 Test Failures
**Date:** 2026-03-07
**Summary:** The builder LLM inconsistently drops `onError` step properties when generating .pr files. Steps like `call GoalThatThrows, on error call Handler` produce .pr with no `onError` property, so errors propagate uncaught. Affects: ErrorCall, ErrorChain, ErrorProps, ErrorTypes, ErrorInHandler tests. Pre-existing tests (Retry, ErrorHandling) still have `onError` because the builder skips unchanged files.

**Fix:** Part of the builder consistency framework. Structural validation (validating .pr output against module registry) should catch this and feed it back to the LLM for retry. Short-term: rebuild these tests and verify the .pr output, or improve the builder prompt to be more consistent about `onError`.

**Related: LLM rewrites assertion values.** The CacheDynamicKey test asserts `%result2% equals "content1"` (intentionally — documenting that cache returns stale data). The builder LLM changed the Expected value to `"content2"` because it thinks it knows better. The structural validator needs to catch when the LLM changes literal values in assertions — the `.goal` file says `"content1"`, the `.pr` must say `"content1"`.

---

## Action-Based Conditions
**Date:** 2026-03-07
**Summary:** Redesign `condition/if` so Left and Right sides of a condition can be actions, not just literals. Since runtime2 supports multiple actions per step, conditions compose naturally with any module action.

**Design:**
- `if file.txt exists` → Left: `file.exists` action (returns bool), implicit `== true`
- `if select count() from users > 0` → Left: `db.select` action (returns number), Operator: `>`, Right: literal `0`
- `if %x% > %y%` → Left: resolved variable, Operator: `>`, Right: resolved variable
- Compound: AND/OR over multiple conditions

**Why:** Current `bool Condition` requires the LLM to pre-resolve everything at build time. Action-based conditions let any module participate without the condition module knowing about it. Architect to design the Condition type, .pr format, and builder prompt changes.

---

## Move Builder to Runtime2
**Date:** 2026-03-11
**Context:** The builder currently runs on v1 runtime (`PlangModule/Program.cs`). Module-specific build-time validation (e.g., file.read checks static paths exist) requires Runtime2 features (action-based conditions, `Left/Operator/Right` format). Can't write validate `.goal` files that run on v1.

**What to do:**
- Migrate builder pipeline (`Build.goal`, `BuildGoal.goal`, `ApplyStep.goal`) to run on Runtime2 engine
- Replace `[plang] ValidateActions` with Runtime2 action(s)
- Enable module-specific validate `.goal` files (e.g., `system/modules/file/validate/validateRead.goal`)
- Rebuild system `.pr` files with Runtime2 builder

**Continue on branch:** `runtime2-builder-validation` — shelved until this migration is done. That branch will add the validate goals and wire them into ApplyStep.

---

## Builder Consistency Framework
**Date:** 2026-03-06
**Details:** [builder-consistency-framework.md](builder-consistency-framework.md)
**Summary:** Structural validation on every build (validates .pr against module registry, feeds errors back to LLM for self-correction). Golden eval suite on-demand for benchmarking LLM accuracy. Architect to design and implement.

---

## ~~ErrorInfoTests still uses "HandlerError" test data~~ ✅ DONE (2026-02-23)
**Date:** 2026-02-23
**Context:** Code analyzer review of runtime2-terminology-fix. Coder renamed all production "HandlerError" → "ActionError" but missed test data in `ErrorInfoTests.cs:198,204`. The `Format_IncludesErrorChain` test uses `"HandlerError"` as error key and asserts `"HandlerError(500)"` in output.

**Fix:** Change `"HandlerError"` → `"ActionError"` and `"Handler error"` → `"Action error"` in the test, update assertion to `"ActionError(500)"`.

---

## Builder Drops onError — LLM Reliability Problem
**Date:** 2026-03-07
**Context:** Builder drops `onError` silently — 5 test suites affected (ErrorCall, ErrorChain, ErrorProps, ErrorTypes, ErrorInHandler). Structural validation can check onError shape when present but cannot detect when it's MISSING.

**Why parser extraction won't work:** There is NO parser for PLang code. The LLM IS the parser. Users can express error handling as "on error call X", "þegar villa kemur", "when something goes wrong" — any language, any phrasing. Deterministic extraction of modifiers from step text is not possible.

**Possible approaches:**
1. **Golden eval suite** (Phase 2 of builder consistency framework) — curated .goal/.pr.golden pairs where onError is known-required. Measures how often the LLM drops it.
2. **LLM-as-judge** (Phase 4) — second LLM validates that error handling intent was preserved.
3. **Prompt improvement** — Stronger examples and instruction language in BuildGoal.llm. Cheapest to try first.
4. **Switch to a more consistent LLM** — Consistency scoring (Phase 3) can identify which LLMs are reliable for onError.

**Not a structural validation problem.** The structural validator checks what the LLM produced, not what it should have produced.

---

## Rename Libraries — Better Name for Action Registry
**Date:** 2026-03-19 (updated 2026-03-20)
**Context:** During crypto module / Providers implementation. With providers moving to a separate `provider` module, Libraries' role is clearer: it's the registry for compiled action handlers (ICodeGenerated). The name "library" is misleading — it implies a broader concept than "compiled step dispatch."

**What to do:**
- Find a better name. Candidates: `compiled`, `assembly`, `actions`, `handlers`? The thing stores compiled C# action handlers and resolves module+action to a handler type.
- Move compiled action handlers (ICodeGenerated) to per-module storage instead of central flat registry.
- Re-evaluate `IAction` interface — source-generated handlers use `ICodeGenerated` + `IContext` + `Run()`. The `IAction.ExecuteAsync` bridge may be unnecessary.
- Libraries' "walk the list, first match wins" resolution might move to per-module resolution.

---

## MemoryStepCache is Per-Instance — Nonce Replay Broken
**Date:** 2026-03-20
**Context:** Signing module architect plan review. `MemoryStepCache` creates a new `MemoryCache` per instance (`Guid.NewGuid()`). If Engine is pooled (one per request), each request gets its own cache, so nonce replay detection won't work across requests.

**Fix:** `ICache` for nonce replay (and likely other cross-request concerns) needs a shared/static backing store, or the cache instance needs to be shared across engine instances within the same app. Check whether `Engine.Cache` is the right level — might need an app-level cache separate from per-engine step caching.

---

## Rename `library` Module
**Date:** 2026-03-20
**Context:** During signing module review. `library.load` currently loads DLLs and discovers compiled action handlers (ICodeGenerated). With providers moving to a separate `provider` module, `library` only handles compiled steps — the name is misleading.

**Question:** What should it be called? `compiled`? `assembly`? `steps`? The module loads external .dll files containing PLang step implementations. "Library" implies a broader concept.

**Related:** "Retire Libraries as Central Action Registry" todo (2026-03-19) — these two may converge.

---

## Sub-Engine Provider Scope Chain
**Date:** 2026-03-20
**Context:** Signing module implementation. Sub-engines don't exist yet — providers are resolved from a single flat registry on Engine.Providers. When sub-engines are added, each needs a local overlay that falls back to the parent's providers.

**What to do:**
- Add parent ref to Providers registry (`_parent: Providers?`)
- Get<T>(name) checks local first, then walks parent chain
- Local overlay cleared when engine returned to pool
- Register on local overlay doesn't affect parent

**4 skipped tests waiting for this:**
- `SubEngine_InheritsParentProviders`
- `SubEngine_LocalOverlay_DoesNotAffectParent`
- `SubEngine_LocalOverlay_ClearedOnPoolReturn`
- `SubEngine_FallsBackToParent_WhenLocalOverlayLacksProvider`

In `PLang.Tests/Runtime2/Core/NamedProviderRegistryTests.cs`.

---

## Investigate ModuleView<T> — Settings Should Expose Properties Directly
**Date:** 2026-03-20
**Context:** During signing module review. `engine.Settings.For<SigningSettings>(context)` returns `ModuleView<SigningSettings>`, which only exposes `Resolve<TValue>("PropertyName", default)`. You can't do `signingSettings.Provider` — you have to do `signingSettings.Resolve("Provider", "ed25519")`. This feels magic-y and the defaults on `SigningSettings` are effectively dead code (never instantiated).

**Problem:** `ModuleView<T>` knows `T` has properties with defaults, but ignores them. The caller must repeat the default value in every `Resolve()` call. If `SigningSettings.Provider` has a default of `"ed25519"`, why does the caller also pass `"ed25519"` to `Resolve`?

**Investigate:**
- Can `ModuleView<T>` instantiate `T` and use its property defaults as fallbacks?
- Can it expose `T`'s properties directly (e.g., via source generator or dynamic proxy)?
- Or should `For<T>()` return `T` itself, hydrated from the scope chain?
- Goal: `signingSettings.Provider` instead of `signingSettings.Resolve("Provider", "ed25519")`

---

## MemoryStack Belongs on Engine, Not Actor
**Date:** 2026-03-22
**Context:** HTTP module design — discussing where `%!Service.Identity%` should be set. Realized: PLang code always runs in User actor context. Variables are always set on User's memory stack. System and Service actors never run PLang steps, so they never need their own MemoryStack.

**Current state:** Each Actor creates a `PLangContext` which creates a `MemoryStack`. System and Service get memory stacks that are never used by PLang code. The only things registered on them (`SettingsData`, `MyIdentity`) are never read from those actors' stacks. `Engine.MemoryStack` already aliases `User.Context.MemoryStack`.

**Question:** Should MemoryStack live on Engine directly (since there's only one that matters — User's)? Or does PLangContext still own it, but System/Service actors skip creating one? Also: can one actor's code path ever accidentally write to another actor's stack? Current isolation says no, but worth verifying as module composition grows.

---

## Rename engine.Settings to engine.Config
**Date:** 2026-03-22
**Context:** During HTTP module design. Discovered naming confusion: `engine.Settings` is the in-memory scope chain (runtime config), while `modules/settings` is persistent db storage. They do different things but share a name.

**What to rename:**
- `engine.Settings` → `engine.Config`
- `SettingsScope` → `ConfigScope`
- `engine.Settings.For<T>(context)` → `engine.Config.For<T>(context)`
- `engine.Settings.Set(key, value, context)` → `engine.Config.Set(key, value, context)`
- `engine.Settings.Defaults` → `engine.Config.Defaults`
- `ModuleView<T>` stays (it's generic enough)
- `ISettings` interface → `IConfig`? Or keep `ISettings` since modules implement it?

**Does NOT change:**
- `modules/settings/` (persistent db key-value store) — stays as "settings"
- `%Settings.ApiKey%` — stays, this is the persistent store
- `SettingsData` bridge on MemoryStack — stays, it bridges to the persistent store

---

## LLM Rewrites Literal Values in Assertions
**Date:** 2026-03-07
**Context:** CacheDynamicKey test asserts `%result2% equals "content1"` (intentionally — documenting that cache returns stale data). The builder LLM changed the Expected value to `"content2"` because it "knows better".

**Problem:** Structural validation can't catch this — the parameter name and type are correct, only the VALUE is wrong. The LLM is semantically modifying the developer's intent.

**Possible approaches:**
1. **Golden eval suite** (Phase 2 of builder consistency framework) — curated .goal/.pr.golden pairs catch value drift
2. **Literal preservation rule** — if a parameter value in the step text is a quoted literal (`"content1"`), the .pr must preserve it exactly. This could be a structural check IF we can reliably identify quoted literals in step text.
3. **LLM-as-judge** (Phase 4) — second LLM validates that values weren't rewritten

**Not for this round.** Needs golden eval infrastructure or a reliable literal extraction heuristic.

---

## Remove `Targets` from EventBinding — move mock tracking into mock module
**Date:** 2026-03-24
**Context:** Events folder cleanup. `Binding.Targets` is `List<object>` — only used by `mock.action` to tag bindings with `MockHandle`, and `mock.reset` to find/remove them.

**Problem:** Weak typing (`List<object>`) on a core Engine class, only to support one module. The mock module already stores `EventBindingId` on the handle — it could track its own binding IDs internally instead of tagging the core Binding.

**Fix:** Add a `List<string> _mockBindingIds` to the mock module (or a shared mock state object). `mock.action` adds the ID, `mock.reset` iterates its own list. Then delete `Targets` from `Binding/@this`.

**Blocked by:** Need PLang `.goal` tests for mock/reset to validate the change doesn't break anything.

---

## File Access Permission System — Out-of-Root Path Security
**Date:** 2026-03-26
**Context:** During file module cleanup. Accessing paths outside the PLang app root (e.g., `read ../file.txt`) should require explicit user permission.

**Design:**
- When runtime resolves a path outside root, throw `FileAccessException` prompting the user
- User responds: `y`/`n`/`a` (always), or natural language like "yes, for 30 min"
- Response creates a signed `PermissionGrant` with expiry, stored persistently
- On subsequent access, check grant store before prompting

**Grant structure:**
```
{ type: "filesystem", path: "../file.txt", access: "read", expiry: "2026-03-26T15:00:00Z" | null, grantedAt: "...", signature: "..." }
```

**Key design points:**
1. **Lives at IPLangFileSystem level** — not in the file module. All modules that touch paths go through the filesystem abstraction, so it's the natural choke point.
2. **Read vs write distinction** — write outside root is more dangerous than read. Grants should specify access type.
3. **Path granularity** — exact file, directory, or glob pattern? Start simple (exact path), expand later.
4. **Signing** — app identity key signs the grant. Expiry embedded in signed payload prevents tampering. "Always" = `expiry: null`.
5. **General permission system** — same prompt/sign/store/check cycle applies to network access, env vars, system commands. Design as a general `PermissionGrant` system, file access is the first type.
6. **Natural language parsing** — "yes, for 30 min" needs LLM interpretation or constrained fixed options (y/n/a/30m/1h/1d).
7. **Storage** — `.build/permissions.json` or similar, with signed entries.

**Separate workstream from file module cleanup.**

---

## Sensitive Data Permission System
**Date:** 2026-03-26
**Context:** During identity module cleanup. `identity.export` returns the full IdentityData including `[Sensitive]` PrivateKey. The `[Sensitive]` attribute already excludes it from output serialization, but there's no runtime permission prompt when PLang code accesses sensitive fields.

**Design:**
- When PLang code accesses a `[Sensitive]` property (e.g., `%identity.PrivateKey%`), the runtime should prompt the user for permission before returning the value.
- Same prompt/sign/store/check pattern as the file access permission system — user responds y/n/always, response is signed with expiry.
- Ties into the general `PermissionGrant` system designed for file access. Sensitive data access is a permission type alongside filesystem and network.

**Separate workstream from identity module cleanup.**

---

## Async variable resolution in source-generated property getters
**Date:** 2026-03-24
**Context:** Source generator creates lazy property getters that resolve `%var%` via MemoryStack. Property getters can't be async in C#. Currently `SettingsData` and `IdentityData` use sync-over-async (`GetAwaiter().GetResult()`) which works in PLang's sequential model but is tech debt.

**Problem:** Any `Data.GetChild()` that crosses an async boundary (database, HTTP, vault) forces sync-over-async. Today it's Settings and Identity. Tomorrow it could be any Data subclass with an async backing store.

**Idea: AsyncData marker + runtime helper**
- The source generator doesn't know the runtime type of a variable — it only sees the target property type (`string`, `int`) and the raw `%var%` pattern.
- But the **runtime resolution code** the generator emits does have the actual `Data` instance after `memoryStack.Get()`.
- Introduce `IAsyncData` (or `AsyncData` base class) as a marker on Data subclasses that need async resolution.
- Generator emits calls to a runtime helper method instead of inline resolution. The helper checks: is this `IAsyncData`? If yes, await an async `GetChildAsync()`. If no, sync `GetChild()` as today.
- `CodeGeneratedExecuteAsync` is already async, so it can await the helper.
- No name-pattern matching (`%Settings.*%`), no fragile detection — the type self-declares its async nature.

**See also:** `Documentation/Runtime2/good_to_know.md` §IdentityData sync-over-async, `plan/phase1/1.9-variable-resolution-v1.md` §async implications.

---

## Per-Module Build-Time Validation Hooks
**Date:** 2026-03-27
**Context:** During LLM module design. Current `ValidateActions` only checks action existence, resolves GoalCall paths, and fills defaults. No per-module validation — e.g., file.read can't warn if file doesn't exist, db.select can't validate SQL.

**Design:**
- Two validation layers: C# interface (`IBuildValidate`) + optional PLang .goal files (`system/modules/{module}/validate/`)
- C# runs before and after PLang validation (pre-validate, post-validate?)
- Validation returns warnings (non-blocking) or errors (triggers LLM retry)
- `BuildContext` provides build-time resources (file system, settings store) without full engine
- Actions opt in — most actions don't need build-time validation

**Examples:**
- `file.read` → warn if file path doesn't exist
- `db.select` → validate SQL against schema
- `goal.call` → warn if goal not found (already done)
- `http.request` → warn if URL malformed
- `crypto.hash` → warn if algorithm not in known list

**Note:** Ingi recalls having designed before/after C# validation + PLang validation in the middle. May have been discussed in a prior conversation. Check for prior design work.

**Separate workstream from LLM module.**

---

## PLang-Defined Modules — `define` Step + Convention-Based Discovery
**Date:** 2026-03-27
**Context:** During LLM module design. Realized the C# action handler for `llm.query` is just a type definition that calls a PLang goal. Pushed further: if the action is just metadata + a goal, why have C# at all?

**Core idea:** Modules can be written entirely in PLang. No C# class, no source generator, no JSON config. Convention-based:
- **Module name** = folder name under `system/modules/` (e.g., `system/modules/llm/` → module `llm`)
- **Action name** = goal file name (e.g., `query.goal` → action `query`)
- **Type/parameter definitions** = a `define` step in the goal file

**The `define` step:**
```plang
Query
- define %action% = { Messages: list(Role: string, Text: string?, Image: string?), Tools: list(GoalCall)?, Schema: string?, Model: string?, Temperature: double = 0.0, MaxTokens: int = 4000, MaxToolCalls: int = 10, Cache: bool = true }
- read settings 'llm.provider', default='OpenAi', write to %provider%
- call goal /system/modules/llm/providers/%provider%
```

- The builder LLM parses the `define` step text into structured JSON in the .pr file
- Inline type definitions: `list(Role: string, Text: string?)` defines a type shape right in the definition
- Defaults are part of the definition (e.g., `Temperature: double = 0.0`)
- Required vs optional: nullable types (`string?`) are optional, non-nullable are required

**Builder integration:**
- At build time, builder scans `system/modules/*/` for goals with `define` steps
- Collects all action definitions into the action summary (same as C# modules today)
- When a PLang developer writes `system: analyze... user: %text%`, the builder matches it to `llm.query` using the definition
- Examples could be comments or a separate convention in the goal file

**What this enables:**
- PLang modules written in PLang — no C# knowledge needed
- External developers create modules by writing .goal files
- The LLM module is the first candidate (pure orchestration: HTTP + goal calls)
- C# modules (crypto, file, etc.) keep current pattern — they need compiled providers
- Both patterns coexist: engine discovers C# modules from assemblies AND PLang modules from `system/modules/`

**Dependencies:**
- `define` as a new action/step type the builder understands
- Builder discovery of PLang-defined modules
- Engine registration of PLang-defined actions alongside C# actions
- Parameter resolution at runtime without source generator (the goal receives raw action data)

**The LLM module is the first module to use this pattern.** Ship LLM with thin C# wrapper if this isn't ready, then migrate when `define` lands.

## Rename BuildStep → StepBuild (Step.Build)
**Date:** 2026-03-31
**Context:** Naming convention is noun-first, verb-second (OBP pattern). BuildStep should be StepBuild → Step.Build. Same for BuildGoal → GoalBuild.

## BuildGoal → BuildStep guidance with confidence levels
**Date:** 2026-03-31
**Context:** When BuildGoal pass 1 can't fully build a step, it currently just sets `needsDetail: true`. Instead, it should provide guidance text to BuildStep pass 2 — "I think this is file.exists + condition.if, sub-steps mean no GoalIfTrue." This gives pass 2 better context for higher accuracy.

**Idea:** Replace the binary `needsDetail: true/false` with confidence levels or structured hints:
- High confidence: BuildGoal built it fully, skip BuildStep
- Medium confidence: BuildGoal has a guess, pass it as context to BuildStep
- Low confidence: BuildGoal identified module+action but needs BuildStep for parameters
- No confidence: BuildStep gets full autonomy

The guidance text from BuildGoal's LLM would flow into BuildStep's prompt as additional context, improving probability of correct output. This is essentially the first LLM "teaching" the second LLM about what it thinks the step means.
