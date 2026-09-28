## architect — app-systems — 2026-09-26
**Target:** /CLAUDE.md (PLang Syntax)
**Why:** Ingi: "comment above says what next code line does". The parser follows it (lines above a goal's name are the goal's comment, lines above a step are that step's), but 356 of 594 `.goal` files in `os/` and `Tests/` describe the goal under its name, so the description lands on step 0. Bots writing `.goal` files copy that shape.
**Proposed change:**
```
- A comment line (`/ …`) describes the NEXT code line. A goal's description goes above its name; a step's comment goes above the step. Never put a goal's description under its name: it would become the first step's comment.
- A folder's docs in a plang app are `start.md` (as `Start.goal` is the entry), not `readme.md`.
```

## architect — app-systems — 2026-09-26
**Target:** /CLAUDE.md (Runtime2 Conventions, the `app/` lowercase bullet)
**Why:** Ingi ruled plang vocabulary lowercase in C# members too, so the plang path, the C# path and the file path match letter for letter ("it started lowercase with modules … it looked good to have everything in lowercase"). The current text says the opposite.
**Proposed change:** replace "**Property names on `app.@this` stay PascalCase** (`.Cache`, `.Builder`, `.Code`, `.Module`, `.FileSystem`, `.Goal`, etc.) — only the *types* live in lowercase singular namespaces. So `ctx.App.FileSystem.Read(...)` is property access (stays capital); `app.filesystem.@this` is the type." with:
```
**plang vocabulary is lowercase in members too.** Everything plang can reach is a lowercase member (`app.type`, `app.goal`, `app.variable`, their `list`, `current`, `all`, `on`, and the facts a face shows), so `%!app.type["text"]%` ↔ `app.type["text"]` ↔ `app/type/type/this.cs`. C# plumbing plang never navigates stays PascalCase; a C# keyword keeps its `@` (`app.@event`); an item's own `Type` (its type entity) stays.
```
Addendum (same day): **nodes lowercase, verbs PascalCase.** Lowercase covers what plang navigates (properties: systems, `list`, `current`, `all`, `on`, facts). Methods stay PascalCase (`Start()`, `Value()`, `Add()`): plang never calls a verb through a path, so the uppercase marks the line where C# executes (Ingi).

## coder — v2 — 2026-09-27
**Target:** /CLAUDE.md (Runtime2 Conventions: "Action `Run()` returns are typed", "Handler naming")
**Why:** stage 1 made `Start` the entry verb of everything that runs (Ingi): every handler's `Run()` is now `Start()`, the generated dispatcher is `ICodeGenerated.Start()` (implemented explicitly beside the handler's own), and goal/step/action/list `Run(context)` are `Start(context)`. The CLAUDE.md bullet still says `Run()`.
**Proposed change:** in the bullet "**Action `Run()` returns are typed via the signature.**", replace `Run()` with `Start()` (and "Action `Run()` returns" in its good_to_know cross-reference title). Add to "Handler naming": "An action's entry method is `Start()`; the generated dispatcher is `ICodeGenerated.Start()`, an explicit interface member, so it shares the name in one partial class — start an action through `action.Start(context)`, never the handler directly."

## coder — v6 — 2026-09-27
**Target:** /CLAUDE.md (Runtime2 Conventions)
**Why:** stage 6 found 11 separate definitions of a `%reference%` (regexes in Formal, step Cover/Scope, pick, debug, text's render, Data's full-match, the store's path tokenizer, the variable's hand scan) that disagreed on spaces, `!` and brackets. They are now one parser, and the .pr carries each row's parsed variables. A new regex for `%…%` would bring the drift back.
**Proposed change:**
```
- **A reference has one definition: `app.type.item.variable.parser`.** Never write a regex (or a hand scan) for `%…%` — ask the parser (`new parser(text).Variable`, `.Read(at)` for one at a position, `.Path()` for a path from a value), or ask the value (`item.Variable` / `HasVariable`). A variable is `Text` + `Code` (its hops: `variable`, `property`, `index`, `method`); it reads through `Start(context)` and writes through `Set(value, context)`. The variable store (`context.Variable`) takes root names only — a path goes through a variable. A stored row writes its `"variable"` list; an authored marked row without it is PrFormatOutdated. The python twin of the parser is `tools/decider/variables.py`; change both together.
```

## coder — v8 — 2026-09-27
**Target:** /CLAUDE.md (Runtime2 Conventions, "No `Console.*` writes in production C#")
**Why:** 8c's E removed the channel list's passthroughs (`WriteTextAsync(name, text)`, `WriteAsync(name, data)`, `ReadTextAsync`, `Resolve`, `Channel(name)`): the list selects, the channel writes. The rule's example still names `app.CurrentActor.Channels.WriteTextAsync(global::app.channel.@this.Output, ...)`, which no longer exists (and never lived on `channel.@this`).
**Proposed change:** replace the user-facing-chatter example with:
```
User-facing chatter → `await context.Actor.Channel[global::app.channel.list.@this.Output].WriteText(...)` — the list selects (`this[name]` for the defaults, which `Verify` guarantees; `Get(name)` for a user-named channel, null on a miss), the channel writes (`WriteText` / `WriteAsync`).
```

## architect — app-systems — 2026-09-28
**Target:** /CLAUDE.md ("Running plang Tests", and every `Tests/` mention)
**Why:** Ingi, 2026-09-28: "the path should not be Tests/ but Test/ not Modules/ => Module/". Folders are singular (the OBP naming rule), and a test lives at the path of what it tests. The plang test tree is renamed `Tests/` → `Test/`, with every plural folder inside it singular (`Modules/` → `Module/`, …) on app-systems. Filed on Ingi's explicit request.
**Proposed change:** replace `Tests/` with `Test/` throughout, and add under "Running plang Tests":
```
- A test lives at the path of what it tests (`Test/Module/On/Cache/`, `Test/App/Type/`), in singular folders — never in a folder named for a branch, stage or plan.
```

## architect — app-systems — 2026-09-28 (refines the entry above)
**Target:** /CLAUDE.md ("Running plang Tests")
**Why:** Ingi refined the layout: lowercase, and a folder per plan. Filed on Ingi's explicit request.
**Proposed change:** use `test/` (lowercase) instead of `Test/`, and add:
```
- A plan's validation tests live in its own folder, `test/plan/<id>/` (id = the plan's branch), each at the path of what it tests; the plan's index is `test/plan/<id>/start.goal` (each behaviour a comment, the step under it runs its test). Tests that aren't a plan's stay in the concept tree (`test/module/on/…`).
```
