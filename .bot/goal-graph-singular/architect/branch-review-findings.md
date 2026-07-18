# Branch review — pending fixes + decisions for coder (2026-07-18)

From the full-branch ObpScan + review. Grouped SETTLED (do it) vs OPEN (flag to architect, don't guess). The condition cluster has its own file (`condition-decision-answer.md`).

## SETTLED — do these

### 1. Fold these renames into the singular sweep
- **Plural → singular** (sweep already targets most): `Parameters→Parameter`, `Modifiers→Modifier`, `Defaults→Default`, `Errors→Error`, `Warnings→Warning`, `Actions→Action`, `Steps→Step`, `Goals→Child` (D4), `Events→Event`, `Properties→Property`, `Notes→Note`, `Examples→Example`.
- **Redundant-suffix → drop it** (NEW — add to the sweep): `LineNumber→Line`, `PriorText→Prior`, `ReturnType→Return`, `ReturnTypeName→Return`, `ActionName→Name` (already planned). The trailing category word (Number/Type/Name/Text) is noise; the prefix carries the meaning.

### 2. The collection property is a plang LIST FACE, not a raw `List<T>`
When `actions.@this` / `steps.@this` delete: `step.Action` / `goal.Step` return a **native plang list** (items-answer's shape: internal `List<action> _action` + a `list.@this` face), NOT a public raw `List<action>`. A raw `List<action>` throws away the whole point of making the graph plang types. Consequence (accepted): **step and goal get an explicit `Output`** (writing their child lists), same as `action` already did — the item owns its wire; the "no explicit step Output needed" shortcut smuggled reflection back in.

### 3. condition cluster → `Decision` type
See `condition-decision-answer.md`. The four methods (`FirstConditionIndex`/`IsFirstCondition`/`ComputeBranchChain`/`SplitAtConditions`) leave `actions.@this` → a singular `Decision` type in the condition module. **Do NOT re-home them to `step`** (corrects the items-answer table). Land before deleting `actions.@this`.

### 4. ObpScan protocol (standing)
Before deleting or re-homing a type, and on any new type before pushing: `dotnet run --project Tools/ObpScan -- <type>` (~15s).
- **MISPLACED / behavioral VERB+NOUN → escalate to architect, do NOT silently relocate.** (That's the whole condition finding — the tool flags it.)
- PLURAL / REDUNDANT name flags → fix in the sweep, no need to ask.

## OPEN — flag to architect, don't guess

- **`step.Clone` (35 lines)** — hand-rolled god-clone that reaches across step→action→modifier, hardcoding every field the sweep renames. Not on any demolition list. Fate undecided: die into the item-base `Clone` (blocked by the `Goal` backref recursion), or shrink to per-item clones. **Decide before the sweep touches it** (it hardcodes `ActionName`/`Errors`/`Modifiers` — all renaming).
- **`action.Reflect` (61 lines)** — the reflection leaf (Properties/Return). Longest method on the branch. Decompose — but it's live module-discovery code; how it splits is a design call.
- **`step.RunFrom` (30) / `steps.MergeFrom` (21)** — verb+noun. The rename is clear (fold in sweep); whether they want a deeper reshape is open.

## Not damage — for the record
The branch's NEW files (`*.Item.cs`, `property`, `modifier`, the `Decision` type) are clean. Every smell above is pre-existing behavior the branch re-homes — which is exactly why #2 (raw-List) and #3 (re-home-to-step) matter: a re-home that lands on the wrong owner keeps the smell. Check the re-home TARGET, not just "the class dies."
