# Handoff to builder/architect — `DictIsItemKeepsNoOrder` (1 PLang test)

C# 4165/4165, PLang 271/272. The sort-throws half is now fixed per Ingi ("in PLang we
always return error; exceptions are the unexpected"). The remaining blocker is a
builder/serialization regression in `error.handle.Actions`, narrowed below.

## The test
```
DictIsItemKeepsNoOrder
- set %people% = [{"name":"b"}, {"name":"a"}]
- sort %people%, on error call Caught
- assert %caught% is true
Caught
- set %caught% = true
```

## DONE — sort returns an error instead of throwing
`PLang/app/module/list/sort.cs` now catches `Compare.NotOrderableException` and returns
`Data.FromError(ValidationError)` — a dict/mixed list is an EXPECTED data condition, so it's a
returned error, not a thrown exception that escapes the on-error pipeline. C# test renamed
`SortOnListOfDict_Throws` → `SortOnListOfDict_ReturnsError`. (Committed.)

## REMAINING blocker — error.handle.Actions builds as a Data-wrapped native `list`, not `step.actions`
`error.handle.Actions` is declared `Data<step.actions.@this>`. On a FRESH born-native build the
modifier's Actions param serializes differently from the older (passing) tests:

- **Passing** (`DoublePlusDecimal_Errors`, etc.):
  `Actions type = list<action>`, value = raw action dicts `[{module:goal,action:call,...}]`.
- **Failing** (freshly built `DictIsItemKeepsNoOrder`):
  `Actions type = list`, value = Data-wrapped dicts
  `[{@schema:data, name:"", type:"dict", value:{module:goal,action:call,...}}]`.

At runtime `error.handle.Wrap` runs (sort's error is seen), but `Actions?.Value` (the
`step.actions.@this`) comes back empty — the conversion of the Data-wrapped native list →
`step.actions` (→ `action.@this` per element) yields no actions. So `hasRecovery=false`, the
recovery chain (`goal.call Caught`) never runs, `%caught%` stays unset, the original error
propagates, and the test fails.

Root: the born-native generic-list wire serialization wraps each element as a typed `Data` and
labels the param `list` instead of `list<action>` — but a structural action chain doesn't
roundtrip through native-list reconstruction (the same lesson noted earlier: error.handle.Actions
should stay `step.actions`, not become a native list). Two fix directions:
1. **Build/serialize side**: emit `error.handle.Actions` as `step.actions` / `list<action>` with
   raw action dicts (not a Data-wrapped generic `list`), matching the passing tests' wire.
2. **Runtime side (robustness)**: make `As<step.actions>` / the catalog conversion reconstruct a
   `step.actions` from a native `list` of Data-wrapped action dicts (unwrap each row → dict →
   `action.@this`).

## Files
- `PLang/app/module/error/handle.cs` `Wrap` (~L83), `Actions` param (L74, `Data<step.actions.@this>`).
- `PLang/app/goal/steps/step/actions/this.cs` — `step.actions.@this : item, IList<action.@this>` (no Convert hook).
- `PLang/app/goal/steps/step/actions/action/this.cs` — `action.@this : item, module.IDataWrappable`.
- `PLang/app/type/catalog/Conversion.cs` — list-target arm (~L303, `value is IList`; native `list.@this` reaches it via the `item.ToRaw` branch ~L163).

---

## RESOLVED (coder, commit a8cefb5a2)

Root went one level deeper than the conversion: the chain reconstructed but each action's
**nested params came back null** (goal.call NRE'd on a null GoalName). The born-native wire
marks records with `@schema:data`; `As<step.actions>`'s `ToRaw→JSON` round-trip strips that
marker, and the Data re-read needs it → params lost.

Fix: a per-type **`step.actions.Convert` hook** (OBP — the catalog stays arm-free; no
`GetListElementType`/`MakeListSink` central type-switch, per Ingi). It rebuilds each action
field-by-field via `FromWireShape` (reads `{name,value,type}` slots directly, no marker
needed) and passes through already-built actions (the C#-constructed chains). Combined with
sort returning the error instead of throwing, the handler now catches it.

Both suites green: C# 4165/4165, PLang **272/272**.
