# to architect — item 6: `goal.Tag`'s type re-hits born-with-context; ExtractAutoTags/SeedBranchChains have no obvious re-home; parser gaining test-tag semantics is a coupling. Rulings needed.

Branch `goal-graph-singular`. Executing your `node-list-values-answer.md` item 6 (analysis sites → birth facts, delete the walkers). Two pieces landed clean; three need your call before I cut code (I don't want to guess `goal.Tag`'s shape and re-introduce the contamination we just spent the back-ref ruling eliminating).

## Landed (item 6, this session)
- **`getTypes` DELETED** — but NOT via `step.Variable`. Ingi ruled the compile prompt takes types straight from `%!app.type.list%`. So `Start.goal`'s getTypes step became `set %stepVarTypes% = %!app.type.list%`, `CompileUser.llm` iterates the type list, `start.pr` hand-edited (builder can't LLM-rebuild itself). **So `step.Variable` is moot** — no builder-fold birth fact needed. (Caveat: builds clean + `.pr` valid, but not yet verified via a real `plang build` that `%!app.type.list%` renders right.)
- **item 5 (mock)**: BeforeAction lifecycle hands the subject action to the handler; `FindCurrentAction` deleted.

## Q1 — `goal.Tag`'s type: your sketch re-hits born-with-context

Your item-6 sketch: `[Store] public global::app.type.item.list.@this Tag { get; init; }` on `goal`.

But `list.@this` is **born-with-context** (the `:103` throw), and `goal` is a **context-free `.pr` POCO** (no `Context` field — same lifecycle you ruled in `backref-pass.md`). A `= new()` default can't construct a `list.@this` (no parameterless ctor without context). This is the exact wall from the node-list conversion.

The node types I just built (`action.list`/`step.list`/`parameter.list`) solved this by being **context-free list value nodes** (born-context-free protected ctor). So the clean options for `goal.Tag`:
- **(a)** a context-free **`list<text>` node** — but there's no `text.list` concept; I'd either make one (a `list.@this<text>` subclass, like the others) or reuse the base `list.@this` with a context-free birth. Which?
- **(b)** a naked `List<text>` / `List<string>` (serialized via reflection) — simplest, but a naked collection (the smell you'd normally reject).
- **(c)** something else you have in mind.

Given the node-list work, **(a)** feels right and consistent, but "a `list<text>` node just for goal tags" may be over-machinery for a tag list. Your call on the type.

## Q2 — `ExtractAutoTags` / `SeedBranchChains` have no parse-time home

Item 6 says delete `HasSkipTag` + `ExtractUserTags` (→ `goal.Tag` at parse) and delete `ForEachAction`. That handles 2 of discover's walkers. But discover has **two more** that `ForEachAction`-walk and genuinely need **built** actions at discovery time — they can't be parse-time birth facts:
- **`ExtractAutoTags`** (`discover.cs:248`): reads each action's handler `[RequiresCapability]` (reflection on the built action's handler type) + follows static `goal.call` chains. Needs the built action + module registry — not knowable at parse.
- **`SeedBranchChains`** (`discover.cs:300`): walks each step's condition actions to seed branch-coverage chains + follows goal.call chains. Needs built condition actions.

You said "ForEachAction is deleted; every walker re-homes to its correct moment" and "a genuinely ad-hoc one-off read uses the public Data face." For these two, the "correct moment" IS discovery, and the read IS ad-hoc. So do they:
- **(a)** iterate the **public value face** (`foreach (row in goal.Step)` → Data → `Peek()` to typed step/action) — still a walk, just via Data rows? or
- **(b)** keep an internal typed walk (some blessed enumeration on the node), contradicting "Elements dies"? or
- **(c)** re-home somewhere I'm not seeing (a coverage/capability birth fact stamped at BUILD)?

Auto-tags-from-capabilities and branch-chains-from-conditions both smell like they could be **build-time** facts stamped onto the goal/step (like `goal.Tag`), computed once when the builder has the handlers — then discover reads them. Is that the intended home, or do they stay ad-hoc value-face walks?

## Q3 — parser gaining test-tag semantics is a coupling

`goal.Tag` at parse means the **generic `.goal` parser** must recognize `tag this test 'X'` step text (the current `HasSkipTag` regex + user-tag extraction) and stamp `goal.Tag`. That couples the parser to the **test module's** tag convention. Is that the intended coupling (the parser owns "a tag directive is structural"), or should tag extraction be a **post-parse pass owned by the test concept** (so the parser stays generic and `goal.Tag` is populated by whoever owns test semantics)? The `IsSkipTagStep` regex also has a **pinned test** (`discover.cs:225`, "Exposed for tests to pin the boundary") that moves with it.

## Meanwhile
No lifecycle question blocks **6d (debug render → os templates)** or **7 (Validate trilogy)** — I can take 6d next while you rule on the above. Pushing this so you can pick it up from origin.
