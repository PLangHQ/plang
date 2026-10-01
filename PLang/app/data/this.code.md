# data: a reference, settled where it is written

A Data whose value is a reference (`%x%`, `%!goal%`, `%Now%`: `IsVariable`) names another Data. `Settle()` answers
that Data as it is here and now: a pointer to the same value (the item is shared, never read), a computed
(`DynamicData`) computes now, and the result keeps the reference's own name and result flags.

## Entry points

```
data.Settle()                                         data/this.cs:128
├─ not a reference, or failed → itself
├─ Follow(Context)                                     :119  — the name-hop, with the reference's own context; no value door
├─ the miss (nothing named) → no value (NotFound, as Follow answered it), with this one's flags, failed with what
│                               its own door says (VariableNotFound, as reading it anywhere) — asked of a copy,
│                               since the reference may be a program's Data shared by runs. The only settled
│                               answer with no value: told apart from a variable holding a failure.
└─ named.Copy(Name, named's context)                   :608  — DynamicData's Copy computes now (:855)
   + this one's Handled / Returned / ReturnDepth
data<T>.Settle()                                             — the settled Data, retyped whole (From), converts at its door;
                                                               a miss stays no value
```

Callers, the one rule in two places:
- `goal/step/action/this.cs` `Attempt` — every attempt's result (a retry's too) leaves settled, after what is
  bound after its start (the cache keeps the attempt's own) and before the error outcome, so `on error` sees a
  miss: `return %!goal%` is the callee, `%Now%` the moment of the return, a returned call argument the callee's.
  `%!data%` is that result.
- `type/item/variable/list/this.cs` `Set` — `set %y% = %x%` binds what `%x%` names, renamed to `y`; a miss leaves
  `y` unset (a NotFound), keeping nothing of the reference, so a later `set %x%` doesn't reach `y`; an `%x%`
  holding a failure binds that failure.

## Tests

- `PLang.Tests/Runtime/App/Goals/ReturnTests.cs` — the goal, the step, `%Now%`, a call argument, the caller's
  `%!data%`, a returned reference to content not yet read (stays unread, same instance), and `return %missing%`
  (fails VariableNotFound, the next step doesn't run, the step's `on error` catches it).

## Known faults

- **The result's control flags ride on the value's Data.** Returned, ReturnDepth and Handled are carried by hand
  at every place a Data is copied or retyped (`Copy`, `Clone`, `From`, `Settle`, …). They are facts about the
  action's result, not about the value.

# data: truthiness — the one door, and emptiness is its negation

Whether a value is truthy is the value's own answer (`item.IsTruthy`, `item.AsBooleanAsync` for I/O: a path's "does
it exist"). `isempty` is "not truthy" — there is no emptiness check beside it (decision 463). A value that doesn't
exist is not truthy: an absent Data (`IsInitialized == false`) holds the null item, and the null item answers false —
no `IsInitialized` check anywhere on the way. So null, `""`, `0`, `false`, `[]`, `{}` and a missing value are empty;
`"  "` is not (whitespace is content).

## Entry points

```
data.ToBooleanAsync()                                 data/this.cs:600 — THE door
└─ Held()                                              :611 — what this holds, read as a question asks it
   ├─ failed already → Peek() (not read again; the failure stays the answer's)
   ├─ Follow(Context)                                  :119 — a reference: the Data it names; a miss is NotFound
   │                                                         (the null item), with no error
   ├─ named.Value()                                         — a literal parses ([] → list), a template renders,
   │                                                         a file's content is read
   └─ a failure on the way (a parse, a read, a variable holding one) → this Data's, answer the absent item
   then item.AsBooleanAsync(context)                  type/item/this.cs:590 — default IsTruthy (:510)
data.ToBoolean()                                      :591 — sync: Peek().IsTruthy(), no read
data.HasValue                                         :475 — presence ("was it given"), a different question:
                                                             "", 0 and false are present
```

The questions asked through it (`data/Operator.cs`):
- `Operator.Truth(data, context)` :188 — a value's truth as a plang bool: a failed operand (before or on the way)
  is the answer, else the door's bool. Asked by: a condition with no Operator (`condition/code/Default.cs:18,21`),
  `isnotempty` (:49) and `isempty` (:48, negated), each side of `and`/`or` (`Logical` :143).
- `Equal` :196 reads both operands through `Held()`: a reference to nothing is null, so `%event.guest% == null`
  is true; a failed operand is the answer.
- `Ordered` :65 reads through `Compare` (the loud value door): a reference to nothing fails with VariableNotFound,
  and that failure is the answer (`%event.guest% > 5`), not "cannot order". A NotFound Left handed in by
  `list.where` (an item's own missing field) is still no match.

Presence callers (`HasValue`, never truthiness): the generator's typed slots (`Emission/Property/Data/this.cs:161,163`
— a written `false` beats the setting and `[Default]`), http `Body` (`http/code/Default.cs:68`), crypto hash/verify
(`crypto/code/Default.cs:27,97`).

## Tests

- `PLang.Tests/Modules/App/Modules/condition/IfHandlerTests.cs` — through the real read path: a missing property and
  an unset variable are empty; `""`, `[]`, `{}`, `false`, `0` are empty; the `[]` / `{}` literals; a set value
  (and `"  "`) is not; `== null` on a missing property is true; `>` on one is VariableNotFound; `write out` of one
  surfaces VariableNotFound; a failed read (`%broken.a%` over bad json) is MaterializeFailed, not empty.
- `PLang.Tests/Generator/Generator/GivenSlotTests.cs` — `list.split(Empty=false)` stays false.
- `PLang.Tests/Data/App/DataTests/DataTests.cs` — `Truthy_*`, `HasValue_*`.
- `test/plan/app-systems/module/condition/if/MissingIsEmpty.test.goal` — the os bot's report, in plang.

## Known faults

- **`write out` of a missing property** writes the VariableNotFound error as the content and the step succeeds
  (reported to the architect, for the coder).
- `contains`, `startswith`, `endswith`, `in` still open operands through the loud door and swallow its failure
  (a reference to nothing reads as no match).
