# data: a reference, settled where it is written

A Data whose value is a reference (`%x%`, `%!goal%`, `%Now%`: `IsVariable`) names another Data. `Settle()` answers
that Data as it is here and now: a pointer to the same value (the item is shared, never read), a computed
(`DynamicData`) computes now, and the result keeps the reference's own name and result flags.

## Entry points

```
data.Settle()                                         data/this.cs:128
├─ not a reference, or failed → itself
├─ Follow(Context)                                     :119  — the name-hop, with the reference's own context; no value door
├─ the miss (nothing named) → itself, failed through its own door (VariableNotFound, as reading it anywhere)
└─ named.Copy(Name, named's context)                   :608  — DynamicData's Copy computes now (:855)
   + this one's Handled / Returned / ReturnDepth
data<T>.Settle()                                      :708  — the settled Data, retyped whole (From), converts at its door
```

Callers, the one rule in two places:
- `goal/step/action/this.cs` `Attempt` — every attempt's result (a retry's too) leaves settled, after what is
  bound after its start (the cache keeps the attempt's own) and before the error outcome, so `on error` sees a
  miss: `return %!goal%` is the callee, `%Now%` the moment of the return, a returned call argument the callee's.
  `%!data%` is that result.
- `type/item/variable/list/this.cs:93` — `set %y% = %x%` binds what `%x%` names, then renames it to `y`.

## Tests

- `PLang.Tests/Runtime/App/Goals/ReturnTests.cs` — the goal, the step, `%Now%`, a call argument, the caller's
  `%!data%`, a returned reference to content not yet read (stays unread, same instance), and `return %missing%`
  (fails VariableNotFound, the next step doesn't run, the step's `on error` catches it).

## Known faults

- **The result's control flags ride on the value's Data.** Returned, ReturnDepth and Handled are carried by hand
  at every place a Data is copied or retyped (`Copy`, `Clone`, `From`, `Settle`, …). They are facts about the
  action's result, not about the value.
