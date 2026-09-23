
## architect — 2026-07-24
**Target:** /workspace/plang/CLAUDE.md (Runtime2 Conventions) or Documentation/v0.2/good_to_know.md
**Why:** Two laws settled repeatedly on goal-graph-singular (four+ independent rulings each: goal.call one-structure, typed-value-set-reader, node-list-values, wiring-snag/back-ref). Future work in the graph/value area must respect them or the sediment patterns return.
**Proposed change:**

```markdown
- **One structure per type; parse at the boundary.** A type has ONE structure: `Output` writes it, its Reader reads it back, its LLM-emit schema is it. Raw forms (bytes, JsonElement, the scalar string form) become the typed value exactly once, at the boundary where they enter, through the type's own door (Reader for streams, `Create` for values — the value's kind decides which). Interior code receives the typed value or fails loud. A re-parse at a consumer (dict/JsonElement arms, `To<Type>()` helpers, `FromSlots`-style slot readers) is always a patch over a boundary that dropped the type — fix the boundary, never add the arm.
- **The graph is the program; context belongs to the run.** Program structure (goal/step/action + their node lists) never stores run state: no actor context, no child→parent back-refs (`step.Goal`/`action.Step` are deleted — the run carries parentage via `context.Goal`/`context.Step` and the Call frame captured at push), no setters that run after load, no traversal door (no `Walk`/`ForEachAction` — code meets the graph at the right moment: birth facts stamped by parser/builder and filtered by collections (`IsSetup`/`IsTest`/`Tag`/`Synthetic`/`step.Variable`), execution via the lifecycle, presentation via templates over the value face). The node collections own three recursions — `Run`, `Output`, `Validate` — the node is always the iterator of itself; no public typed-element face exists (`.list`/`.Elements` harvesting is the smell; internal face = Add/IndexOf/positional indexer only).
```

## architect — 2026-09-22
**Target:** Documentation/v0.2/good_to_know.md
**Why:** Found during the (abandoned) formal-plang grammar attempt; the doc it lived in is deleted with the abandonment. The rule decides how nested actions, `%var%`, event goals and recovery chains are modelled, and it forbids the lazy-construction pattern that produced the last back-ref stamp on the graph.
**Proposed change:**

```markdown
### Every call is constructed at load; only its evaluation is deferred

A `%var%` is the model: built at load as a Data holding a name, evaluated when read. A nested action inside a value may be the same — constructed at load (so it is born holding its step), evaluated when its owner reads it. What may never happen is lazy CONSTRUCTION of program structure at run time: nobody holds the step at birth, and the graph ends up stamped afterwards.

Four relationships between calls, each with its own wire slot:
- **sequence** — runs next; the left result is `%!data%` (`step.action[]`)
- **wrap** — runs around its host (`action.modifier[]`)
- **body** — runs when the owning call decides (`action.child[]` for condition bodies; `action.recovery[]` for `error.handle`)
- **expression** — runs when the owner reads the parameter (a parameter's value — grammar-legal; no evaluator yet)

Plus **reference** — a value neither evaluated on read nor run by its owner, stored for later (an event handler's goal). Rides as the `goal.call` TYPE, never as a nested action.

A body written as an expression (recovery actions stored in a parameter) and a reference written as an expression (`channel.set Goal=goal.call(…)`) are the two known mis-modellings; both are fixed by moving the value into its correct slot/type.
```

## architect — 2026-09-22 (Stage D)
**Target:** /PLang/App/CLAUDE.md (or Documentation/v0.2/good_to_know.md)
**Why:** Coder hit this twice in one change while moving build validation onto the nodes: `await p.Value() is GoalCall` opened every parameter, an authored `%var%` (unset at build time) failed to resolve, and the failure surfaced three layers later in code nobody touched — the courier had marked the binding failed. It is a general build-time rule, not a Validate rule.
**Proposed change:**

```markdown
- **Build-time code judges SHAPE, never evaluates authored values ("judging must not resolve").** Anything that runs at build time — validation, Nest, Normalize, the repair loops — reads an action's declared types, catalog rows and structural slots, and never opens a parameter's VALUE except on a slot whose declared type is the thing being judged (`p.Type?.Name == "goal.call"` first, ask second). Authored values are constructed at load and evaluated on read; evaluating one at build time does the run's job at the wrong moment, and a failed evaluation poisons the binding for the run that follows — the error lands far from its cause. Corollary: `await p.Value() is X` is not a guard, it is the opened box; check the declared type, then ask.
```

## coder — v1 — 2026-09-23
**Target:** /CLAUDE.md
**Why:** `loop.foreach`'s variable-naming slots were renamed `ItemName`/`KeyName` → `Item`/`Key` (Ingi: "Name" restates what the variable type already says). CLAUDE.md's Property-kinds bullet still names the old slots.
**Proposed change:** in the "Property kinds (PLNG001 build-time gate)" bullet, replace
`` `loop.foreach` ItemName/KeyName `` with `` `loop.foreach` Item/Key ``.

## architect — 2026-09-23
**Target:** /CLAUDE.md — Runtime2 Conventions, directly after "**Registry = selection + lifecycle; all behavior lives on the element** …"
**Why:** Ingi named it "the key in OBP" while ruling on the build pass: the builder-side walker over a step's actions (the build pass in the builder's Default provider) skipped modifiers and recovery, so a goal.call-specific pass grew its own modifier walk beside it. Run, Output and Validate are already node-owned; Build was the holdout, and every future pass will face the same choice.
**Proposed change:**
- **Walks are node-owned.** Every pass over the program graph — `Run`, `Output`, `Validate`, `Build` — is the node iterating itself and recursing into its own children (`Modifier`, `Recovery`, `Child`, and any parameter whose DECLARED type is `action` / `list<action>`). A static or builder-side walker over `List<action>` is the stray helper of walks: it reaches only what its author remembered, and every feature then grows its own walk beside it. The holder says `step.Build(context)`; the node does the rest. This is "all behavior lives on the element" applied to traversal.

## architect — 2026-09-23
**Target:** /CLAUDE.md — OBP Shape Smells, under **Shape:** (beside **stray helper**)
**Why:** Ingi, while ruling the type-registry pass: "anything named in two ways is bad" and "we dont want static in obp". The trigger: `choice<Operator>` answered four ways across the type registry (the value, two copies of the naming rules, the CLR-type door, the reader key), and the registry carries alias pairs (`Name`/`GetTypeName`, `Clr`/`Get`, `ValidValues`/`GetValidValues`, `BuilderNames`/`GetBuilderTypeNames`) plus public statics. The stray-helper line covers only private statics.
**Proposed change:**
- **named twice** — one thing answering to two names: a type with two plang names across doors, a method with an alias, two copies of the same rules (a static and an instance naming function). Every door that answers "what is this called" must give the same answer, and a caller must never have to know which door it asked. Fix: one door per question, the owner's; the alias dies.
- **static** — no `static` members in OBP code, public or private: a static is behavior with no owner. Move it onto the object whose knowledge it is, or delete it. (Exempt: constants, and C#-mandated statics such as `static abstract` interface members and generator entry points.)

## architect — 2026-09-23
**Target:** /CLAUDE.md — Runtime2 Conventions, the bullet "**Action prose lives in markdown, not attributes.**"
**Why:** The bullet describes machinery this branch deleted. The old compiler prompt (`Compile.llm`) that rendered per-action Notes is gone with the old builder; `MarkdownTeaching` (loader, `MergeLayers`, `ScanOrphans`, `ModuleStem`) and the orphan warning are deleted (Ingi, 2026-09-23 — no production caller); module-level notes/examples are dropped (one file existed). Decided with Ingi: the object owns its docs as plain members, and the module knows its folder.
**Proposed change:** replace the bullet's text after the first two sentences with:
```
**Prose** (Description, Notes, Examples) lives in `os/system/modules/<module>/<action>.{description,notes,examples}.md`, plus one `module.description.md` per module. The owners read it as their own members — `module.Description`, `action.Description` / `Notes` / `Examples` — lazy file handles built from the module's `Folder` (the one place the folder rule lives); an action reaches it through the module it was born with. An absent file is falsy (`{% if a.Notes %}` checks presence without reading). No loader class, no orphan scan. `[Description]`/`[ModuleDescription]`/`[Example]` do not exist on action handlers — don't add them back.
```
