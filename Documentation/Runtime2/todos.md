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

## External Library Integration (engine.astro)
**Date:** 2026-02-13
**Context:** During OBP naming work on Lifecycle/Bindings.

**Idea:** `engine.Libraries` — register and call external .dll libraries from PLang.
- `engine.Libraries.Add('my.dll')` — user registers their dll
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

## Libraries Replaces ActionRegistry
**Date:** 2026-02-13
**Context:** Discussing engine as platform — making external DLLs first-class.

**Idea:** `engine.Libraries` replaces `ActionRegistry`. It's a `List<Library>`, lazy-loaded on first access.

- Built-in handlers (variable, file, output, etc.) are Library[0] — everything uniform
- `engine.Libraries.Add("astrolib.dll")` — loads assembly, discovers `[Action]` types
- Handler resolution walks the list: `engine.Libraries.Get("variable", "set")`
- From PLang: `load library astrolib.dll`
- Builder can auto-add libraries when it sees unknown actions
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
