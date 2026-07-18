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

## Resolved (were open — settled w/ Ingi 2026-07-18)

- **`step.Clone` (35) → DELETE.** Zero production callers (grep-verified); only two tests exercise it (`StepTests.cs`, `modifier/ModifierRegistryTests.cs`) — circular, they test the method's existence. Dead code. Delete the method + those two tests. No item-base-Clone reconciliation needed.
- **`action.Reflect` (61) → decompose; the row builds itself (NO static factory).** `Reflect` produces `action.Properties` — the param schema the compiler shows the LLM (rendered by `stepActionDetails.template`). It mixes a filter with field-by-field row construction. Fix: **`property.@this` gains a constructor `new property.@this(PropertyInfo prop, App.Type)`** that reflects its own Name/Type/Nullable/Default/IsVariable (absorbs `UnwrapToValue` + `IsVariableNameSlot`). `Reflect` keeps only the filter loop — skip EqualityContract/capability/`[Code]`, build the row, drop it when `row.Type.Name` ∈ {clr, goal, step, action, modifier}, plus the channel row (~61→~25). `ReflectReturn` (verb+noun, single caller) inlines into the `Return` getter.
- **`RunFrom` → `Resume`, `MergeFrom` → `Merge`.** These are obpv (multi-word method names; the ONLY name exemption is boolean Is/Has — no preposition carve-out). `RunFrom` IS the snapshot-resume mechanism → `Resume(context, fromIdx)`. `MergeFrom` matches the existing `step.Merge` → `Merge(prior)`. Rename on `step`, `goal`, `steps`/`goal.step` collection + the snapshot/build callers.

### Post-Decision landing (2026-07-18)
- **`Decision.HeadIs` → `IsHead`** — my spec error: a boolean's Is/Has must be the PREFIX (the sole name exemption); `HeadIs` is Is-as-suffix = obpv. Rename `IsHead`. (`action.IsFirstConditionInStep` caller updates.)
- `Decision.Labels` (private label-chain builder, single caller `Of`) → inline into `Of`; drops the plural-name flag.

## Not damage — for the record
The branch's NEW files (`*.Item.cs`, `property`, `modifier`, the `Decision` type) are clean. Every smell above is pre-existing behavior the branch re-homes — which is exactly why #2 (raw-List) and #3 (re-home-to-step) matter: a re-home that lands on the wrong owner keeps the smell. Check the re-home TARGET, not just "the class dies."
