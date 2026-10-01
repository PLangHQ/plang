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
   └─ a value reached again while it renders → throws AppException VarResolveCycle   :184-189
```

Where a template renders: `data.Settle` (`data/this.cs`), at a set and at every action's exit — the value is
rendered where it is written. A goal-call argument binds as written in the callee's frame, so it renders there.

## Tests

- `PLang.Tests/Runtime/App/Goals/ReturnTests.cs` — a returned template, a set template, self-append, a missing name.
- `PLang.Tests/Modules/App/Modules/variable/TemplateCycleTests.cs` — an unset self-naming template fails the set; a
  call argument that names itself is a cycle, at a set and at a return.

## Known faults

- **The partial render throws where it could answer a failure** (`:180`, `:189`): an unset variable and a cycle
  are AppExceptions, not a failed answer on the Data. `data.Settle`'s `Rendered` catches
  `VariableNotFoundException` to answer it; that catch goes when text answers its failure on the Data.
