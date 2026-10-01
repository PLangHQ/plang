# text: a template renders through its own door

A text stamped as a template (`"%name%: %price% kr"`) holds its variables; its value door renders it.

## Entry points

```
text.Value(data)                                      type/item/text/this.cs:141
├─ not a template → itself
├─ one whole variable (%x%) → that variable's value, through its own door; unset → fails on data (VariableNotFound)
└─ partial → Rendered(context)                        :165 — one pass: each literal as written, each variable
   ├─ unset %!x% → written as it is                          written through its value's own door
   ├─ unset %x% → throws VariableNotFoundException    :180
   └─ a value reached again while it renders → throws AppException VarResolveCycle — the crash net for this
      door (Start + Output), unreachable today since every binding is settled where it is written
```

Where a template renders: `data.Settle` (`data/this.cs`), at a set, at every action's exit, for a goal call's
parameters, a loop's item and a goal channel's message — the value is rendered where it is written.

## Tests

- `PLang.Tests/Runtime/App/Goals/ReturnTests.cs` — a returned template, a set template, self-append, a missing name.
- `PLang.Tests/Modules/App/Modules/variable/TemplateCycleTests.cs` — an unset self-naming template fails the set; a
  parameter naming its own name renders with the caller's variable, and fails the call when the caller has none.

## Known faults

- **The partial render throws where it could answer a failure** (`:180`, `:189`): an unset variable and a cycle
  are AppExceptions, not a failed answer on the Data. `data.Settle`'s `Rendered` catches
  `VariableNotFoundException` to answer it; that catch goes when text answers its failure on the Data.
