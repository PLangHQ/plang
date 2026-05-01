# docs v2 — runtime2-generator-obp

## What this is

Second docs pass on `runtime2-generator-obp`, after architect/v5 → coder/v7 → coder/v8 (auditor/v3 PASS). The branch's earlier work (the v4 source-generator OBP refactor through coder/v6) was already documented in docs/v1. v7-v8 added a separate change: retire `[VariableName] string` entirely, introduce `App.Variables.Variable` (record) + `IRawNameResolvable` (marker), migrate 22 handler property declarations to `Data<Variable>`, delete the Legacy property emitter, and (v8) add a generator-side guard that restores the pre-v7 `MissingRequiredParameter` ServiceError contract for null variable-name slots. v1's docs treated `[VariableName]` as canonical — those references are now stale.

## What was done

### CLAUDE.md proposal decisions (3 from coder/v7, all applied)

| From | Target | Decision | Where applied |
|------|--------|----------|---------------|
| coder/v7 | `/PLang/App/CLAUDE.md` | applied — folded into root | `CLAUDE.md` line 25, "Property kinds" entry |
| coder/v7 | `/Documentation/v0.2/good_to_know.md` | applied | New "App.Variables.Variable — the variable-name carrier" section |
| coder/v7 | `/Documentation/Runtime2/todos.md` | applied | 2026-04-30 entry marked RESOLVED with the actual approach taken |

Same scoping reasoning as v1 — the `/PLang/App/CLAUDE.md` target file does not exist; folded into root rather than scatter canonical guidance across single-rule per-folder CLAUDE.md files.

### Documentation gaps filled

1. **Root `CLAUDE.md`** — line 21 OBP shape line drops `Legacy/` from the per-property tree. Line 25 replaced with the two-rule contract: `Data<T>` / `[Provider] T`, plus the `Data<Variable>` description for variable-name slots and a forward-pointer to the v8 `MissingRequiredParameter` guard.

2. **`Documentation/v0.2/architecture.md`** — four edits:
   - Catalog attribute model line: dropped `[VariableName]` from the attribute list, added a one-liner that variable-name slots are now typed via `Data<App.Variables.Variable>`.
   - Source-generator tree diagram: dropped `Legacy/this.cs`, added the parenthetical "(incl. `Data<Variable>` name-slots)" on the `Data/this.cs` row.
   - Property kinds table: row for `[VariableName] string` removed; PLNG001 message updated to the new two-shape form.
   - New paragraph after the table covering Variable + IRawNameResolvable + the v8 missing-name guard, with cross-references to data-generic-design.md and good_to_know.md.

3. **`Documentation/v0.2/good_to_know.md`** — three edits:
   - "Action property kinds (PLNG001 build-time gate)" section: bullet for `[VariableName] string` removed; PLNG001 message updated; "Why the gate exists" rewritten to mention v7's removal of the Legacy emitter and `[VariableName]`; "Currently exempt" block deleted (no exemptions remain).
   - New section "App.Variables.Variable — the variable-name carrier" added immediately after, covering: what it is, why it exists, the implicit-conversion gotcha (`var s = name.Value` infers `Variable` not `string`), the nullable-foreach variant, `WasPercentWrapped`'s purpose, and the v8 missing-name guard with the noted empty-string-slot follow-up.
   - "Attribute matching is short-name only" note for `[Sensitive]`: removed the `[VariableName]` cross-reference (the attribute no longer exists).

4. **`Documentation/v0.2/action-catalog.md`** — three edits:
   - Property attribute table: `[VariableName]` row removed.
   - Type shape table: new row for `Data<App.Variables.Variable>` describing how it renders in the catalog (as `string`, with a `%var%` value).
   - Fully-annotated `variable.set` example: `[VariableName] partial string Name` replaced with `partial Data<Variable> Name` and the comment block updated to describe the IRawNameResolvable dispatch and `.Value` use site.
   - Two trailing string-literal mentions of `[%var% string]` (the old type tag in catalog rendering) corrected to `[string]` per the actual post-v7 catalog output.

5. **`Documentation/Runtime2/todos.md`** — 2026-04-30 entry marked **RESOLVED 2026-05-01** with a one-paragraph summary of the actual approach (Variable + IRawNameResolvable, not VarRef<T>) and the v8 guard. Original design discussion preserved below as archived context.

### XML docs

Spot-checked the new public surface — coder's XML on `App.Variables.Variable` (the type, the single-arg ctor, the implicit `string` operator, `Resolve`, `ToString`) and `App.Variables.IRawNameResolvable` is excellent: covers what + why, names the dispatch path, calls out the symmetry rule. Added none.

### CHANGELOG additions

User-visible surface change summary in `result.md`:
- `[VariableName]` attribute removed (reflection-based code in third-party PLang extensions would break — none expected; PLang itself audited)
- New public types: `App.Variables.Variable` and `App.Variables.IRawNameResolvable`
- New ServiceError key: `MissingRequiredParameter` now also fires for absent `Data<Variable>` slots (and any future `Data<T>` where `T : IRawNameResolvable`)
- Catalog rendering: variable-name slots now render as `Name([string] %var%)` instead of `Name([%var% string])`

## Code example — what changed in handlers

```csharp
// before (pre-v7)
[VariableName]
public partial string ListName { get; init; }

// in Run()
var data = Context.Variables.Get(ListName);
return Error(new ValidationError($"List '{ListName}' missing"));


// after (v7+)
using App.Variables;

public partial Data.@this<Variable> ListName { get; init; }

// in Run()
var data = Context.Variables.Get(ListName.Value);
return Error(new ValidationError($"List '{ListName.Value}' missing"));
//                                                ^^^^^^ Variable.ToString() == Name
```

The interpolation site reads `ListName.Value` and lets `Variable.ToString() => Name` print the canonical name in the error string. Method-call sites (`Variables.Get(...)`) get `string` via the implicit operator on `Variable`. The 22 migrated handlers all match this pattern.

## Findings

- **1 minor** flagged for tester (not blocking) — same shape as v1 finding: no PLang `.goal` example covers the `MissingRequiredParameter` ServiceError surface for variable-name slots. PLang `.goal` examples are the tester's job; flagging for visibility.

## Verdict

**PASS** — all gaps filled, all three CLAUDE.md proposals applied, no coder clarification needed. Ready to merge.
