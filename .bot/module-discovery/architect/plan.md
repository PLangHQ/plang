# Stage 4 — module-discovery: the collection at `app.module`, teaching as templates

Settled with Ingi 2026-07-15. Supersedes the shape parts of `coder/stage4-plan-seed.md` (the seed's decomposition survives; the model corrections below override it).

> **You (coder/test-designer) own the final code and test shape.** Shapes below pin intent, not lines. Every `file:line` was checked against `c1e4f8e10`; re-verify at implementation time.

## Why

`module.@this` is the last god-object: it owns action dispatch AND teaching (`Describe()` — imperative reflection assembling prompt text in C#, `StepActions` string blobs, `BuildTypeEntries(modules)` feeding the type views). One way for each thing: **selection via the module collection, teaching via element structure + Fluid templates, prose via the elements' own markdown.** Deleting the three teaching members unblocks Stage 5's sweep (3 of its remaining `[Obsolete]` marks), retires `goal.getTypes` (the string-typed shadow of the type system), and makes the builder prompt user-editable. 4f applies the same presentation rule to the test report and deletes the bespoke junit serializer (yagni).

## The settled model

1. **`module` and `action` are HOSTS, not plang types** (Ingi ruled; item ⟺ ICreate decides it — they are never authored, never created from values). Carried as `clr(module)`/`clr(action)`, navigated by the reflection kind, rendered through `[Out]` faces, read by Fluid directly. Same family as `app`/`goal`. The catalog action is **the same `action` host the `.pr` uses, at class-level zoom** (parent plan's ruling) — trace item: verify the `.pr` action's step-level state doesn't leak into the class-zoom face; if the two zooms fight, surface it, don't fork the type.
2. **The collection is the node: `app.module`** — `app/module/list/this.cs` (`app.module.list.@this`), hand-rolled like `goal.list`, mutable (`code.load` adds at runtime). Selection `app.module["http"]`; dispatch reads through the element (`module.Actions["request"]` answers the handler type). Registry = selection + lifecycle; behavior on the element. The old `module.@this` dissolves into collection + elements over 4a–4e.
3. **The property is lowercase: `app.module`** — Ingi killed the "properties on `app.@this` stay PascalCase" rule (singular + lowercase, both). This plan uses lowercase for the NEW node; the global rename of existing properties (`.Cache`, `.Goal`, `.FileSystem`, …) is its own mechanical pass, NOT this branch (logged in the close-out; CLAUDE.md proposal filed in `.bot/module-discovery/claude-md-proposals.md`).
4. **One handler walk.** The collection's population walk over handler classes does every per-handler job in one pass: builds the module/action elements AND registers choice closed sets + their `Reader<T>`s. `RegisterModuleChoiceTypes` (verb+noun obpv, `type/list/this.cs:483`, called from `app/this.cs:307`) dissolves into that walk — no public compound-name method survives.
5. **Reflection once, at the leaf.** `action.Properties : list<type>` is the ONLY reflection site — lazy, first touch, with exactly `Describe()`'s current filters (capability interfaces `IContext/IStep/IChannel/IEvent/IStatic`, `[Code]` props, `EqualityContract`), unwrapping `Data<T>`/`[Code] T`/`Nullable<T>` to the plang `type` entity. Consumers read `type.Name`, never a `System.Type`, never `GetTypeName` at a call site.
6. **Teaching is 100% templates — no C# assembles prompt text.** The views hand structure; Fluid hands ALL text. Any C# StringBuilder/string-interpolation in the teaching path dies with `Describe()` — none gets grandfathered as "glue".
7. **Prose lives on the elements.** `MarkdownTeaching` (static helper class + compound name, both obpv) dissolves: `module`/`action` own loading their prose (`module.Description`, `action.Description`/`.Notes`/`.Examples` read `os/system/modules/<module>/…md` themselves; the orphan scan becomes the collection's own audit at population). Nothing central remains.
8. **Template format = the extension.** The error-page precedent (`os/system/error/<code>.<ext>`) generalizes: same stem, extension picks the format — `report.txt` beside `report.json`; `ui.render` resolves stem + requested format; mime from the extension via `app.Format`. Inside a `.json` template, values embed via the Fluid `json` filter, and that filter MUST route through our json writer — the one-JSON-producer rule; verify what Fluid's stock filter does and rewire if it serializes on its own.

## The pieces, in landing order

4a–4c land BEHIND the living `Describe()` (both alive, suite green); 4d adds the templates; the parity gate passes; 4e switches and deletes; 4f rides the same template machinery.

- **4a — `module` host + `app.module` collection.** Element: name, its actions, its prose doors. Collection population = the one handler walk (model #4 — absorb the choice/Reader<T> registration here, delete `RegisterModuleChoiceTypes` in the same commit). `app` news it once at the lowercase property.
- **4b — `module.Actions : list<action>`.** The class-zoom face on the existing `action` host: owning module, name, handler `System.Type` as a PRIVATE field (never leaks). Includes the zoom-conflict trace from model #1.
- **4c — `action.Properties : list<type>`** — the reflection leaf per model #5, keyed by property name, plus the prose doors (model #7).
- **4d — Templates + builder goals.** `os/system/builder/templates/modules.md` (drill-ins by filter); builder goals `get all modules → %modules%`, `ui.render 'templates/modules.md' → %doc%`. Structure from the elements, prose from their markdown doors. Extension convention per model #8.
- **PARITY GATE (blocks 4e):** render the builder prompt via `Describe()` and via the template; assert equality (or pin a golden of today's prompt first). Teaching drift is this stage's silent-failure mode — the Stage-1 field-drift lesson applied to prompts. Compile-quality is the real acceptance: the golden must cover a module WITH markdown prose, one WITHOUT, a `[Code]`-bearing action, and a choice param.
- **4e — Repoint + delete.** Callers move to the elements; then `Describe()`, `StepActions`, `BuildTypeEntries(modules)`, `goal.getTypes` die (the parent plan slates getTypes for exactly this stage — the compile prompt's per-step scope types come from the views/type entities, never name strings).
- **4f — Test report via `ui.render`.** Render `list<test-result>` through templates pulling exactly what the report reads (test id ← `Goal.Path`, `Status`, `Duration`, `Error`, `Stdout`). **junit is DELETED, not templated** (Ingi: yagni) — only the formats actually consumed get templates (start `report.txt`; `report.json` only if something consumes it — verify who reads the report today before authoring a second template). `module/test/report.cs` + `app/test/junit/this.cs` die. The WriteReflected host-lift (cluster-3) STAYS as the general safety net — 4f just stops the report leaning on auto-serialization.

## Leaf trace — incumbents and their callers (verify counts at implementation)

| Incumbent | Known callers | Disposition |
|---|---|---|
| `Describe()` (`module/this.cs:297`) | `build/code/Default.cs:24,77` | callers read the elements/template output; method dies 4e |
| `StepActions` | 11 hits: `GlobalUsings`, `module/this.cs`, `kind/list`, `kind/reflection`, `build/code/IBuilder.cs`, `build/code/Default.cs`, `build/validateStepActions.cs` | each site repoints to `module.Actions`/`action.Properties`; type + alias die 4e |
| `BuildTypeEntries(modules)` | `type/list/this.cs`, `type/list/view/this.cs`, `type/this.cs`, `Attributes/PlangTypeAttribute.cs` | the type views read `action.Properties`; the (modules) overload dies 4e — the module-less catalog fold STAYS (it serves `app.Type`, not teaching) |
| `goal.getTypes` (`module/goal/getTypes.cs`) | action registration only; `primitive/this.cs:68` comment | action deleted 4e; comment updated |
| `MarkdownTeaching` (`module/MarkdownTeaching.cs`) | `module/this.cs`, `goal/steps/step/actions/action/this.cs` | body dissolves onto module/action prose doors (4a/4c); file + name die |
| `RegisterModuleChoiceTypes` (`type/list/this.cs:483`) | `app/this.cs:307` | dissolves into the 4a population walk; both sites die 4a |
| `module/test/report.cs`, `app/test/junit/this.cs` | test module | die 4f; junit not replaced |

## Demolition (what must NOT survive, by stage)

- **4a:** `RegisterModuleChoiceTypes` + its `app/this.cs:307` call.
- **4a–4c:** `MarkdownTeaching` class, name, and both call sites.
- **4e:** `Describe()`, `StepActions` (type, alias, all 11 sites), `BuildTypeEntries(modules)` overload, `goal.getTypes`, every C# prompt-text assembler in the teaching path, the `.pr`-era doc comments naming them (`action/this.cs:115,123` "Populated by Modules.Describe()").
- **4f:** `module/test/report.cs`, `app/test/junit/this.cs`, `test.report.Wire`'s reflected-dump reliance.
- **Stays:** the module-less `BuildTypeEntries(null)` catalog fold; action dispatch behavior (relocated onto collection/elements, not removed); the per-module markdown FILES (they are the prose source — only their loader moves); `WriteReflected`'s host-lift; `app.Module`-era compat NOT kept — no forwarder property.

## Acceptance

- Baseline held: ~195 reds, by-name diff vs the branch-start snapshot; zero new.
- Parity gate green (prompt equality / golden), then the compile suite + `plang build` Sanity goals still progress past the pinned error (clean rebuild first — the stale-binary trap).
- Grep gates at 4e: `Describe(`, `StepActions`, `BuildTypeEntries(_modules`, `getTypes`, `MarkdownTeaching`, `RegisterModuleChoiceTypes` → zero production hits.
- The long-tail cluster "List cannot lower" (2 reds) disappears with getTypes.

## OBP validation

| Surface | Check | Verdict |
|---|---|---|
| `module`/`action` as hosts | item⟺ICreate honored; no ICreate on system structure; clr carriage | ok |
| `app.module` collection | collection node at the concept, selection + lifecycle only; dispatch via element | ok |
| one handler walk | no second walk for choices; registration is population's own job | ok |
| `action.Properties` lazy leaf | reflection once, on the owner; `System.Type` private | ok |
| prose doors on elements | no static helper class; loader dissolved onto owners | ok |
| templates own all text | no C# text assembly; presentation = templates (settled rule) | ok |
| extension-as-format | one stem, ext picks format; matches error-page precedent; json filter → our writer | ok |
| names | `module`, `action`, `Actions`, `Properties`, `Description`/`Notes`/`Examples` — nouns on owners, no verb+noun | ok |

## Plang-type leaf audit

Hosts hold plain C# (`string Name`, `List<action>`) — correct per the host rule; the plang-typed surface is where plang meets them: `action.Properties` yields `type` ENTITIES (never name strings), durations/counts in the 4f report ride as their plang types in the template model (`Duration` = duration, not a preformatted string). Flag any leaf the implementation adds that answers a raw CLR name.
