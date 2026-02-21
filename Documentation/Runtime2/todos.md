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

## Safe cast in Settings.Resolve<T>
**Date:** 2026-02-21
**Context:** Code analyzer found `Settings.Resolve<T>` uses hard cast `(T)value` (lines 34 and 40 in `Engine/Settings/this.cs`). If a setting value is stored with the wrong type (e.g., int instead of long), this throws InvalidCastException instead of falling through to the class default.

**Fix:** Replace `return (T)value;` with `if (value is T typed) return typed; else continue;` (or fall through to classDefault). One-line change, two locations. Add a test for the type mismatch scenario.
