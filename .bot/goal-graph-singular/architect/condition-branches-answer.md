# Condition orchestration — the four methods become a `Branches` type on condition (settled w/ Ingi 2026-07-18)

**Corrects the items-answer re-home table.** That table sends `Chain`/`Branches`/`FirstConditionIndex`/`IsFirstCondition` from `actions.@this` → `step`. **Do not.** That relocates a misplaced smell to another wrong owner. The real fix below.

## The finding

`condition.if.Orchestrate` (if.cs:70) legitimately owns branch control-flow — condition.if IS the control-flow action, it reaches the step's actions via context, it's the owner. `Orchestrate` **stays**.

The problem is the four helper methods on `actions.@this` — `SplitAtConditions`, `ComputeBranchChain`, `FirstConditionIndex`, `IsFirstCondition`. They're condition.if's logic (callers: `condition.if` + `test.discover` only), and their **verb/compound names** (`Split…`, `Compute…BranchChain`) are the tell that the branch layout wants to be a **type**, not something recomputed each call.

## The shape — a `Branches` value

Build the step's condition-branch structure **once** as a `Branches` object, owned by the condition module. It owns:
- the split into `(condition, body)` branches (was `SplitAtConditions`),
- the if/elseif/else label **chain** (was `ComputeBranchChain`),
- the first-condition index / is-first (was `FirstConditionIndex` / `IsFirstCondition`),

as its own properties/navigation. Then:
- `condition.if.Orchestrate` **walks** a `Branches` instead of calling three methods.
- `test.discover` **reads** `Branches` (chain, first index) off the same object.
- the four methods **dissolve** into constructing one `Branches` + reading it.

Home: the condition module (e.g. `app/module/action/condition/branches/this.cs` — `Branches` built from the action sequence + the head index). Not `step`, not `actions.@this`, not an abstract "orchestration" type.

## Consequence for the collection deletion

With the four methods gone to `Branches`, `actions.@this` sheds its condition logic and can delete clean. **Sequence: build `Branches` and move the logic BEFORE deleting `actions.@this`** — otherwise the deletion cements the smell on step per the old table.

## ObpScan — it exists; use it (protocol)

Built at `Tools/ObpScan` (spec: `Documentation/v0.2/obp-scan-tool.md`). Run: `dotnet run --project Tools/ObpScan -- <type-substring>` (~15s).
- **When:** before deleting or re-homing a type, and on any new type before pushing.
- **The rule that matters:** a **MISPLACED** or behavioral **VERB+NOUN** flag is a **design call — flag it to architect, do NOT silently relocate it.** (This whole condition finding is what that rule catches — the tool flags `SplitAtConditions` VERB+NOUN + MISPLACED.)
- **PLURAL / REDUNDANT** name flags (`Parameters→Parameter`, `LineNumber→Line`) → fix in the singular sweep, no need to ask.

The division: the tool hands candidates; you fix the mechanical names, escalate the ownership/behavioral ones. Stops "delete the class, move the method to the next wrong owner."
