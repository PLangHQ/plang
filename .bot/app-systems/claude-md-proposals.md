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

## coder — v10 — 2026-09-28
**Target:** /CLAUDE.md
**Why:** Stage 9a (decision 175/176) collapsed path's read verbs: `ReadText`, `ReadBytes`, `ReadAsBase64`, `ReadAsDataUri` are gone; `path.Read` lands a reference and its content is that reference's own value (raw bytes via `IContent.Content`). The System.IO rule's verb list names the deleted verbs, so a bot following it would reach for methods that no longer exist.
**Proposed change:** in the "No `System.IO.*` reaches in production C#" bullet, replace the verb list
```
(`ReadText`, `WriteText`, `List`, `Stat`, `ReadBytes`, `ExistsAsync`, `MoveTo`, `CopyTo`, …)
```
with
```
(`Read`, `WriteText`, `WriteBytes`, `List`, `Stat`, `ExistsAsync`, `MoveTo`, `CopyTo`, …) — `path.Read` lands a reference (`file`/`url`/`directory`); its content is the reference's value, its raw bytes `IContent.Content`
```

## architect — app-systems — 2026-09-28
**Target:** Documentation/v0.2/obp-smells.md (Value layer) and /CLAUDE.md (OBP Shape Smells, Value layer)
**Why:** Ingi, 2026-09-28, on the llm query's own cache: "this is obpv. I dont want to fix it now, I want to be learning oppertunity, so we can then spot them when we do full sweep on the code base." CLAUDE.md forbids enveloping Data ("Data is not enveloped"), but the smell catalog has no name for it, so a review can't cite it by name and the sweep has no tell. The specimen is kept unfixed on purpose; the worked example, with checked grep tells, is `.bot/app-systems/architect/plan/obp-example-llm-cache.md`. Filed on Ingi's explicit request.
**Proposed change:** add to obp-smells.md under Value layer, and one line to the CLAUDE.md quick list:
```
**envelope** — a hand-built wrapper around Data's shape, usually to carry what `[JsonIgnore]` keeps off the wire or out of the store: `new Dictionary<string, object?> { ["Value"] = …, ["Model"] = … }` stored, then unpacked by name (`entry.Name == "Value"`). The field list gets written at every site that packs or unpacks it, and the value is often kept twice (read and raw). Fix: store the Data whole and mark what should persist for the view (`[Out]`/Store); if a module wraps Data to cache it, it is doing the cache's job. Tells: `\["Value"\]` or `Name == "Value"` outside `app/data/`; a comment saying code goes around `[JsonIgnore]`. *Worked example (kept unfixed as the sweep's specimen):* the llm query's own cache (`llm/code/OpenAi.cs`, `RestoreFromCache`).
```

## architect — app-systems — 2026-09-28
**Target:** /CLAUDE.md (Runtime2 Conventions, near "Lazy params")
**Why:** Stage 9b turned every action handler into a one-line door to the owning type, and two signature styles grew side by side: list members taking carriers (`list.At(data<number>)`) and number members taking values (`n.Round(number)`). Decision 231 settles it. Without a written rule, each module picks its own style again.
**Proposed change:**
```
- **Owners take values; handlers open carriers.** A type's operation takes plang values (items: `number`, `text`, `path`), never `data<T>` carriers and never CLR primitives. The action handler opens its carriers through `Use` (`Path.Use(p => Pattern.Use(pat => p.List(pat, …)))`), whose failure is the action's answer; for several optional settings, the generated `Given()`. The CLR lowering happens only at the real boundary inside the owner (the System.IO call, the .NET API).
```

## architect — app-systems — 2026-09-29
**Target:** /CLAUDE.md (Runtime2 Conventions, after "Data is not enveloped")
**Why:** Stage 12a (decisions 286–289) found one fact, "a program's error that travels as an exception", carried four ways (`AppException` key+status, `OutputException`/`NormalizeException` key on a JsonException, `DeclinedException` an Error on an InvalidOperationException) and rebuilt at seven catches, each its own way; foreign exceptions got two keys ("ServiceError" at three catches, "Exception" from `Error.FromException`), and a key built from the C# class name leaked into plang. It came back twice in new code during the same stage (the http signing error and OpenAi's image read cracked a caught Error to prefix their subject). Without a written rule, every new throw site re-invents the translation.
**Proposed change:**
```
- **A program's error answers; it travels as an exception only where no result can.** A method that can return a result answers `Fail`/`FromError` with the error's own key. Only where no result can be returned (a constructor, a stream callback, a member with no Data return) does it throw — an `app.error.AppException` (or subclass) carrying its `Error` whole: `throw new AppException(error)`. Every catch answers `ex.Error` (a `catch (AppException ex)` clause, no `is`/`as`/`switch` inside a general catch). `Error.FromException(ex)` is the one door from any exception to an Error: a carried one comes back whole, anything else is `ServiceError` (500) naming its C# type in the message — never a key built from the class name. **Never crack a caught Error** into message/key/status to rebuild it: answer it whole, or make your own Error holding it as its cause (`Error.list`); the frame's `Record` already stamps where it happened. A throw that stays a plain .NET exception means plang itself is broken (a constructor guard, a boot invariant).
```

## architect — app-systems — 2026-09-29
**Target:** /CLAUDE.md (Runtime2 Conventions, the "Data is not enveloped" bullet, its last sentence)
**Why:** The bullet says "new domain types ship by adding `[Out]` to the properties that should cross the wire". For an `item` subclass that wasn't true: item's base `Output` was the leaf path (`Write`, throwing for a structure), so every structural item repeated `Output => new reflection().Output(this…)` (six copies), and a new one tagged only `[Out]` threw. Decisions 324–330 made the base answer it (a leaf writes bare, a structure writes its tagged bag through the reflection kind), made a type that writes its own flat form say so once (`Output => Write`), and made a type from the assembly with no face for a view refused on the wire and written by name in a dump (`Tagged.Declares`). Found while fixing inherited tests; the rule is canonical for every new item type.
**Proposed change:** replace the bullet's last sentence with:
```
The value slot is built via `data.Normalize(View) → IWriter`, so a new domain type ships by tagging the members that should show: `[Out]` (the wire), `[Store]` (persisted), `[Debug]` (a dump). An `item` structure is then written as that tagged bag by the base `Output` — no override, and **do not** add a `JsonConverter`. A type that writes its own flat form (a path's location, an error's shape) states `Output => Write` once on its base. A type from the assembly that tags nothing for a view is refused on the wire (`NoWireContract`) and written by its name in a dump — a runtime structure (actor, binding) is never walked.
```

## architect — app-systems — 2026-09-30
**Target:** /CLAUDE.md (Runtime2 Conventions, a new bullet after "Data is not enveloped")
**Why:** Ingi, 2026-09-30, reviewing image's `Write` (`switch (writer.Format) { case "text": … case "protobuf": … default: base64 }`) and 317's `if (mode == View.Out)` load inside a value's own `Output`: "you can see immediately that something is wrong when Writer has case statement, that is a serializer. also when there is an if statement, that usually means wrong structure." The old line in `item.Write`'s doc ("OBP Rule 9: the value owns its wire shape, the writer never type-switches") let a value serialize itself per format. He approved the replacement below (decision 335). Filed on his explicit instruction ("do that new rule and update the doc").
**Proposed change:**
```
- **A value writes what it is; the writer decides how it looks.** A value writes itself through the writer's primitives (`String`, `Number`, `Bytes`, …); the writer (the formatter) decides how each primitive looks in its format. Neither asks about the other: no `writer.Format` in a value (no `switch`, no per-format table), no value type in a writer. A value's content is opened at the last moment by the layer that sends it out (the channel), never inside the value's own write, and never by view (`if (mode == View.Out)` in a value is the wrong structure).
```

## architect — v1 — 2026-09-30
**Target:** CLAUDE.md, the "Action `Run()` returns are typed via the signature" bullet
**Why:** The bullet says bare `Task<Data>` is "only for actions that produce no value (no `→ returns` line; compile LLM rejects trailing `write to %x%`)", and in the same bullet says polymorphic forwarders (`goal.call`, `llm.query`) stay on bare `Task<Data>`. The two disagree, and the code follows the second: `goal/step/action/this.Schema.cs:61` maps bare `Task<Data>` to the return `item`, so `write to %x%` is valid. Found by the coder building decision 400 (`output.ask` became a forwarder on bare `Task<Data>`; its catalog return line is `item`).
**Proposed change:**
```
- bare `Task<Data>` only for actions that produce no value (no `→ returns` line; compile LLM rejects trailing `write to %x%`).
+ bare `Task<Data>` for forwarders that return a Data produced elsewhere (`goal.call`, `llm.query`, `output.ask`, condition evaluators); the catalog reads it as `→ returns item` (`goal/step/action/this.Schema.cs:61`).
```

## architect — v2 — 2026-09-30
**Target:** CLAUDE.md, the "Action prose lives in markdown" bullet under Runtime2 Conventions
**Why:** The bullet names `MarkdownTeaching.ScanOrphans` and `PLang/app/module/MarkdownTeaching.cs` as the loader and orphan scan. Both were deleted in 2349faf11; the action's docs are now lazy file items on the catalog element (`goal/step/action/this.Schema.cs:69–86`: `Description`, `Notes`, `Examples`), read by the builder's templates (`os/system/builder/llm/templates/properties.template`). Found by the fix bot tracing the generated module pages (decision 411), where the spec said to reuse the scan.
**Proposed change:**
```
- Orphan files surface as warnings via `MarkdownTeaching.ScanOrphans`. Full guide: `Documentation/v0.2/action-catalog.md`; loader: `PLang/app/module/MarkdownTeaching.cs`.
+ The action's docs are lazy file items on its catalog element (`goal/step/action/this.Schema.cs`: `Description`, `Notes`, `Examples`), read by the builder's templates (`os/system/builder/llm/templates/`). Full guide: `Documentation/v0.2/action-catalog.md`.
```

## architect — v3 — 2026-10-01 (on Ingi's request: "I want to highlight the dot case learning … a rule")
**Target:** CLAUDE.md, "OBP Shape Smells", a new first line under **Shape:**
**Why:** Ingi, designing `list.query`: a compound camelCase name is a missing hierarchy. Writing it as a dot path and checking that each segment navigates in the code shows where the thing lives and when the structure is wrong. The architect sketched `ListName` (copied from `list.where`/`sort`/`group`); as a dot path, `list.name`, it doesn't navigate: a list has no name. The name is the holding variable's, `list.variable.name`, so the action takes `list`. Ingi: "if you do that correctly, it will guide you through where things should be and when things don't match … a rule."
**Proposed change:**
```
- **glued name** — a compound camelCase name (`ListName`, `buildExecutionPath`). Write it as a dot path, one word per segment (`setting.build.execution.path`), and check it navigates: each segment an owner whose member is the next. A path that doesn't navigate (`list.name`: a list has no name) is a flat copy or a misplaced member; the dot path shows the real owner (`list.variable.name`, so take `list`).
```

## builder — v1 — 2026-10-01
**Target:** CLAUDE.md, the "Action prose lives in markdown, not attributes" bullet under Runtime2 Conventions
**Why:** The bullet describes the retired two-phase pipeline ("the user message of each Compile call only when the planner picked that action; Compile.llm keeps only the cross-cutting kernel") — there is no planner/Compile call on app-systems; it's the decider + one whole-goal writer (Properties) call. It also links `Documentation/v0.2/action-catalog.md`, which I moved to `os/system/modules/catalog.md` (co-located, renamed off `.code.md` per Ingi — no `catalog.cs`). The `MarkdownTeaching.cs` loader part is already covered by architect's v2 proposal above, but that proposal's replacement text still points "Full guide" at the old `Documentation/v0.2/action-catalog.md` — this supersedes that path.
**Proposed change:**
```
- Per-action Notes render in the user message of each Compile call **only when the planner picked that action**; `Compile.llm` keeps only the cross-cutting kernel. `module.*.md` is a reserved stem (module-wide teaching layer); the renderer concats module-first + blank line + action. Orphan files surface as warnings via `MarkdownTeaching.ScanOrphans`. Full guide: `Documentation/v0.2/action-catalog.md`; loader: `PLang/app/module/MarkdownTeaching.cs`.
+ Per-action Notes/Examples render in the writer's Properties call (one `llm.query` per goal) **only for the actions the decider listed for a step**; `os/system/builder/llm/Properties.llm` keeps only the cross-cutting kernel. Each catalog element exposes its teaching as lazy file items (action: `PLang/app/goal/step/action/this.Schema.cs`; module: `PLang/app/module/this.cs`) under `os/system/modules/<module>/` — an absent file is falsy; there is no load-time orphan scan. Full guide: `os/system/modules/catalog.md`.
```

## architect — v4 — 2026-10-01
**Target:** characters/builder/character.md, line 22 (the C# boundary) and line 125
**Why:** Ingi ratified it directly to the architect (2026-10-01, decision 452: "yes, correct"), then narrowed the C# to modules (relayed by the builder bot the same day): a build bug sent to the builder bot can be fixed along its path (prompt, template, teaching, and the module C# that feeds them) instead of handing the C# half to the coder; core runtime stays the coder's. The character still says "minor C# changes only".
**Proposed change:**
```
- You do not own user-facing `.pr` files or runtime C# (that's coder). Minor C# changes are allowed when they directly expose data the builder's goal files need but can't currently reach — note them clearly when you make them.
+ You own all of `os/system/**` (the builder's goals, `.llm` prompts and templates, and the action teaching under `os/system/modules/**`) and the module C# under `PLang/app/module/**` (`module/build`, `module/llm` and the decider among them). Core runtime is the coder's and you don't edit it: `PLang/app/goal/**` (the pick, the step checks in `goal/step/this.Validate.cs`), `PLang/app/type/**`, `event`, `actor`. A build bug whose fix lies in core goes to the coder through the architect. Build bugs reported by any bot come to you. Show the architect the shape before a C# edit; every commit gets the architect's review and gate.

- - **Missing data** → minor C# change to expose what the builder needs
+ - **Missing data** → the change in a module's C#; in core, ask the coder through the architect
```
