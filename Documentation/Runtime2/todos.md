# Runtime2 Todos

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
| 1 | **system.sqlite** | NOT STARTED | System database. Ingi bringing details. |
| 2 | **Setup.goal** | NOT STARTED | How DB and store are set up. Depends on system.sqlite. |
| 3 | **Settings** | IN PROGRESS | Goal-scoped module settings. On `runtime2-settings` branch. |
| 4 | **Variable persistence** | NOT STARTED | Storing and loading variables (to/from system.sqlite). |
| 5 | **Input action** | NOT STARTED | Console input (`input.read`). Output exists, input doesn't. |
| 6 | **Pluggable action implementations** | NOT STARTED | Actions like templating should allow plugging in any rendering engine. Architecture for swappable implementors behind a stable action interface. Same pattern needed for DB, crypto, etc. |
| 7 | **Retry testing** | DONE | Covered by `Tests/Runtime2/ErrorRetryOnly/` (bare + timed retry with attempt counting) and `Tests/Runtime2/ErrorGoalFirst/` (GoalFirst order). Added on `runtime2-builder-onerror-fix`. |

**Dependency chain:** system.sqlite → Setup.goal → variable persistence. Settings is parallel. Input and pluggable implementations are independent. Retry is independent.

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

## Builder Consistency Framework
**Date:** 2026-03-06
**Details:** [builder-consistency-framework.md](builder-consistency-framework.md)
**Summary:** Structural validation on every build (validates .pr against module registry, feeds errors back to LLM for self-correction). Golden eval suite on-demand for benchmarking LLM accuracy. Architect to design and implement.
