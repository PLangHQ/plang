## builder — v1 — 2026-06-06
**Target:** /workspace/plang/CLAUDE.md
**Why:** Debugging the `assert → error.throw` build regression on this branch, I had
to grep the codebase to discover that PLang's template engine is **Fluid** (a .NET
Liquid implementation) and where its variable-binding lives. That fact is load-bearing
for any builder/`.llm`/template debugging: the builder's whole prompt-construction
layer (`*.llm`, `*.template`) renders through Fluid, and Fluid binds PLang values
differently from PLang's own resolver — it can read real .NET `List<T>`/`Dictionary`/
POCOs but **not** native `dict`/`list`/`JsonNode` (they lack `IEnumerable`/
`IDictionary`). That mismatch silently renders template sections empty and was the
root cause of a branch-wide builder failure. Knowing the engine + this trap up front
would have saved a long investigation; it'll recur for anyone touching templates.
**Reference:** full debugging runbook for this failure class —
[`Documentation/v0.2/debugging-builder-failures.md`](../../../Documentation/v0.2/debugging-builder-failures.md).
**Proposed change:** add to the "Key Files" section (or a new "Templating" note):

```
## Templating (Fluid)
- The template engine is **Fluid** (a .NET Liquid implementation). All `*.llm` and
  `*.template` files render through it via `ui.render` (`app.module.ui.render`).
  Binding lives in `PLang/app/module/ui/code/Fluid.cs` — variables are loaded with
  `FluidValue.Create(variable.Value, options)` in the render loop.
- **Fluid does NOT use PLang's variable navigator.** It can read real .NET
  `List<T>`/`Dictionary`/POCOs, but NOT PLang native `dict`/`list`
  (`app.type.{dict,list}.@this`) or `System.Text.Json.Nodes.JsonNode` (what
  `set … type=json` produces) — those implement domain interfaces, not
  `IEnumerable`/`IDictionary`, so `{{ x.key }}` / `{% for i in x %}` render EMPTY
  even though `%x.key%` resolves fine in PLang. If a value resolves in `--debug`
  but renders blank in a template, this is why.
- Debugging a wrong `.pr`: see `Documentation/v0.2/debugging-builder-failures.md`.
```
**Footer:** Filed by reviewer/builder bot under the "real incident on the branch"
exception (root-caused a branch-wide builder regression to this gap).

## coder — v1 — 2026-06-06
**Target:** /workspace/plang/CLAUDE.md (`## OBP Shape Smells`)
**Why:** The smell list governs collections/refs/values but says nothing about *type names*. The "one clean word" rule in `obp-smells.md` is stated only for *methods* (verb+noun), so two-word type names slip through — `LlmMessage`, `ToolCall`, `GoalCall`, `CacheSettings` are live violations. A qualifier in a type name is the type-level form of the same rule and, when the bare word "collides," a smell-#3 detector. Add one terse item with a grep-able tell.
**Proposed change:** add as item #8 under `## OBP Shape Smells`:

```markdown
8. **Type name carries a qualifier.** A two-word type (`LlmMessage`, `ResponseCache`, `HttpConfig`) where one word is its namespace or an existing concept. Types are one domain word; the namespace disambiguates (`http.Config`, never `HttpConfig`). Three fixes, never "keep the name": (a) qualifier is the namespace → move to the concept folder, drop the prefix (`message` in `app.module.llm`); (b) the bare word collides with an existing concept → namespaces still separate them (`module.cache` vs `llm.cache`), so the name stays one word — the collision is only a concept-identity question (smell #3); (c) it's a variant → file-per-variant (`path` → `path.file`). Tell: a CamelHump in a type declaration.
```

## coder — v2 — 2026-06-06
**Target:** /workspace/plang/CLAUDE.md (`## Build`)
**Why:** When `plang build` fails, the diagnostic playbook isn't discoverable from CLAUDE.md — bots retry blindly instead of reading the failure-triage doc. Add a one-line pointer.
**Proposed change:** add under `## Build`:

```markdown
- When a build fails, read `Documentation/v0.2/debugging-builder-failures.md` before retrying — it's the failure-triage playbook (planner vs compiler vs template-binding). Inspect a built `.pr` with `python3 tools/pr-summary.py <path|folder>` (step→action mapping); inspect an LLM trace with `python3 Documentation/v0.2/inspect-trace.py <trace>`.
```

## coder — v3 — 2026-06-06
**Target:** /workspace/plang/CLAUDE.md (`## Key Files` or `## Build`)
**Why:** Bots (me included) repeatedly hand-write throwaway python to parse a `.pr` and check the builder's step→action mapping. A committed read-only summariser (`tools/pr-summary.py`) makes that one command, and pairs with the existing trace tool. Pointing to both from CLAUDE.md stops the one-off-script habit.
**Proposed change:** add under `## Build`:

```markdown
- After a build, check the mapping with `python3 tools/pr-summary.py <path-to.pr|folder>` — one terse line per step (`index | text -> module.action [+modifier]`); flags dropped modifiers and empty action lists. Read-only; never hand-edit a `.pr`.
```

## coder — v4 — 2026-06-06
**Target:** /workspace/plang/CLAUDE.md (a new `## Coder role` note, near `## Running plang Tests`)
**Why:** The coder/test-builder role and its done-bar weren't written down, so sessions drifted (deferring whole suites, leaving stubs red, treating builds as someone else's job). Ingi's standing instruction: the coder writes AND builds the plang tests, and a session is only done when both suites are green.
**Proposed change:** add:

```markdown
## Coder role — the done-bar
- The coder writes AND builds the `.test.goal` files (not just authors them). A goal isn't "done" until it builds to a correct `.pr` (`plang build --build='{"files":[...],"cache":false}'`, then verify with `tools/pr-summary.py`).
- **Hand over 100% passing at session end — both suites.** `dotnet run --project PLang.Tests` (C#) and `cd Tests && plang --test` (PLang) must be fully green before the session closes. No red stubs left behind, no "deferred to the tester" for builds you can run. If a behaviour genuinely can't land, the test is removed or quarantined with an explicit, surfaced reason — not left failing.
```

## coder — v5 — 2026-06-06
**Target:** /workspace/plang/CLAUDE.md (the builder v1 "Templating (Fluid)" note, once applied)
**Why:** The builder's v1 proposal documents the Fluid "native dict/list/JsonNode render EMPTY" trap as a live failure. That is now FIXED in `Fluid.cs` (a ValueConverter wraps natives in lazy read-through views — `{{ x.key }}` / `{% for %}` work; member access stays O(1), no copy). The CLAUDE.md note should describe the trap as handled, not open, or a future bot will "re-fix" it.
**Proposed change:** when applying builder-v1's Templating note, replace the "render EMPTY" bullet's conclusion with: "Native `dict`/`list`/`JsonNode` ARE now Fluid-readable via a lazy read-through ValueConverter in `Fluid.cs` (`NativeDictView`/`NativeListView`) — no copy, O(1) dict member access. If you add a new native value type that must render in templates, extend that converter."
