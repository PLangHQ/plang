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

## A number as text — the writer, born with a culture

```
text.Encode (this.cs:47) / text.Rendered          — the two places a text writer is made, each with the asker's
  new text.Writer(stream, encoding, culture)         context.Setting.Of<app.setting>().Culture (%!app.setting.culture%,
                                                      the machine's when unset; type/item/culture: a name no culture
                                                      has is refused at the set, UnknownCulture)
text.Writer.Double/Decimal/Float (writer.cs)       — a bare number: Written(value)
  at most the culture's decimals (NumberDecimalDigits, 2 for most), trailing zeros dropped, its separator
  (7,47 in is-IS); a non-zero value keeps its first significant digit (0.001, never 0); **no grouping**
  (1234567.89, not 1,234,567.89) — a decision, not an accident
inside a container (depth > 0)                     — json (Structural): whole and invariant, as json and the wire
```

A number with a decimal point is a double (`number/this.Parse.cs`); a decimal is asked for by name (`as decimal`).

## Tests

- `PLang.Tests/Types/App/Types/NumberTextTests.cs` — 2.49 × 3 as text (7.47), as json (7.470000000000001), is-IS
  (7,47, also set from plang), 0.001, 3, a culture no one has fails the set.
- `PLang.Tests/Runtime/App/Goals/ReturnTests.cs` — a returned template, a set template, self-append, a missing name.
- `PLang.Tests/Modules/App/Modules/variable/TemplateCycleTests.cs` — an unset self-naming template fails the set; a
  parameter naming its own name renders with the caller's variable, and fails the call when the caller has none.

## Known faults

- **Two render doors, two cycle guards.** A variable renders through its value door (`variable.Value`: Start,
  then the value's `Value()`, guarded by the depth count) and a template writes each variable through `Output`
  (`Rendered`: Start, then `Output`, guarded by the rendering set). When rendering has one door (the writer says
  how), one guard goes.

- **The partial render throws where it could answer a failure** (`:180`, `:189`): an unset variable and a cycle
  are AppExceptions, not a failed answer on the Data. `data.Settle`'s `Rendered` catches
  `VariableNotFoundException` to answer it; that catch goes when text answers its failure on the Data.
