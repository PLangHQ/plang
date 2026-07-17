# Demolition worklist — what the graph→items change orphans (for coder, run at the END)

Traced against pushed HEAD `a39bd42a6` (increments 1-2 landed: graph is items, transitionally — `Output` still delegates to the reflection kind, no per-type readers yet, collection classes still alive). **Do NOT delete anything here until increment 3 lands (explicit `Write` + `serializer/Reader.cs` per level + the `.pr` read flipped off the reflection kind).** Each item names its gate and a verify. Grouped by *when it becomes safe to delete*, not by file.

> **You own this.** I traced it; the compiler + your test deltas are the truth. Anything that doesn't actually orphan — leave it and tell me.

## Gate 1 — after per-type readers land (increment 3)

1. **The two obsolete bridge readers → deleted or replaced:**
   - `goal/serializer/Reader.cs` — the `[Obsolete]` stub that reflects goal and wraps `clr<goal>`. Your new `goal/serializer/Reader.cs` (the real item reader) REPLACES its body; the `clr<goal>` return and the stale `GoalReadOptions` doc-reference go with it.
   - `goal/steps/step/actions/serializer/Reader.cs` — the `[Obsolete]` `actions`-collection reader wrapping `clr<actions>`. **Deleted, not replaced** — a native list of `action` items has no bespoke collection reader; each element reads through `action`'s own reader. Verify no `(actions, kind)` registry entry is still looked up.

2. **The transitional faces you added in increments 1-2 → replaced by the real ones:**
   - The delegating `Output` (routes graph items to the reflection `*` kind) — replaced by the explicit token `Write`/`Output`. Don't keep both.
   - The reflect-`Set` override (opens the Data door, `value.Clr(propType)`) — re-verify it's still needed once the item's own `Set`/navigation exists; if the item handles child-write natively, the reflect-`Set` goes.

3. **The `WriteReflected` BRIDGE case** — `type/item/this.cs:532-533`, whose own comment reads *"a raw C# collection (goal.steps / action.modifiers / action.parameters …) … **Deleted once they are items.**"* The graph collections are items now. **Verify before deleting:** `action.Parameter`/`Default` stay `List<data.@this>` (host storage) — confirm they don't ride this bridge case (they should write via the `@schema:data` path, not the raw-IEnumerable arm). If params still need it, the case shrinks to params-only rather than dying; inventory the arm's live inputs first.

## Gate 2 — after the collection classes delete + methods re-home (increment 3 tail)

4. **The three collection classes, whole:**
   - `goal/steps/this.cs` (`steps.@this`), `goal/steps/step/actions/this.cs` (`actions.@this`), `goal/steps/step/actions/action/modifiers/this.cs` (`modifiers.@this`) — deleted; children are properties on the items (internal typed list + native-list face). Members re-home per `items-answer.md`'s table.
   - **`actions.Value => _items`** (the raw-storage naked-collection leak) — dies with the class; its callers were already sentenced to read through the face.
   - The `IList<T>` facade bodies, the `Count` collision question, the `private protected _items` seam FOR THE GRAPH — all moot (no subclassing happened; the seam survives only for `error.list`/`warning.list`).

5. **GlobalUsings aliases** (`PLang/app/GlobalUsings.cs:4-9`):
   - `GoalSteps` (`app.goal.steps.@this`), `StepActions` (`…actions.@this`), `ActionModifiers` (`…modifiers.@this`) — **die** (classes gone).
   - `Step`, `ErrorOrder`, `CacheSettings` — **re-point** to the new singular namespaces (`app.goal.step.@this`, `app.goal.step.ErrorOrder`, …), not deleted.

## Gate 3 — after the goal-read flip (`clr<goal>` → `Data<goal>`, the 33-site cascade you sized)

6. **The `clr<goal>`/`clr<action>` carrier usage for the graph** — 51 grep hits across ~13 files. This is the RISKY one (your note: `(x as clr<goal>)?.Value` goes silently null at runtime, not a compile error). Most are *re-points* (`as clr<goal>` → the goal item), not deletions — but once the flip is complete:
   - `clr.@this<goal>` / `clr.@this<action>` **closed-generic constructions** for the graph should have zero remaining sites — grep-gate it.
   - The generic `clr.@this<T>` type itself STAYS (genuine hosts like `app` still use it — `app/this.cs:430`); only the graph's use of it dies.
   - Entry points you already named: `goal/list/this.cs:372` `LoadFromFileAsync`, and every `(… as clr<goal>)?.Value` site — audit each against a test delta, not the compiler.

7. **The reflection-kind read path FOR THE GRAPH** — `ReadDataList`/`IsListOfData`/`ElementTypeOf` in `type/item/kind/reflection/this.cs` no longer receive goal/step/action types (they read through their own `ITypeReader`s now). **The reflection kind STAYS** (it serves real hosts — `app`, foreign `[Store]` DTOs); do NOT delete it. Only verify the graph types no longer route through it (a `goal.Steps`-shaped read should hit the goal reader, never `ReadDataList`). If nothing else feeds `ReadDataList` after the graph leaves, THAT is a separate finding — flag it, don't assume.

## Gate 4 — after the singular sweep + rebuild

8. **Stale doc-comments naming the dead shapes** — the `goal/serializer/Reader.cs` comment references `GoalReadOptions` and `.bot/read-path-unification/…`; the `actions` reader comment references `action.FromWire`/`FromWireShape`/the `Convert` hook. If those symbols are already gone, the comments are the last trace — they die with the readers (gate 1). Grep the tree for `FromWire`, `FromWireShape`, `GoalReadOptions`, `clr<goal>` in comments after the sweep; zero expected.

## Grep gates for the close-out (all must hit zero in production)

```
app.goal.steps            # namespace — gone
steps\.@this|actions\.@this|modifiers\.@this   # the classes — gone
clr\.@this<.*goal|clr<goal>|as clr<goal>        # graph carriers — gone (clr<T> for app stays)
GoalSteps|StepActions|ActionModifiers           # dead aliases — gone
\.Value\b in actions                            # the naked leak — gone
FromWire|GoalReadOptions                         # stale references — gone
```

## What explicitly SURVIVES (don't over-delete)

- `type/item/clr/this.cs` (the `clr<T>` type) — genuine hosts use it.
- `type/item/kind/reflection/*` (the `*` kind) — serves hosts + foreign DTOs.
- `error.list`/`warning.list` subclasses + their `private protected _items` seam.
- `Parameter`/`Default` as `List<data.@this>`; the `@schema:data` reader path.
- `goal.call`'s own reader (`goal/call/Reader.cs`) — a different concern.
- The reflection `Set`/`Get` base machinery — hosts still navigate through it.
