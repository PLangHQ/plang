# goal.step: whether an answer holds what the step says

A step is built from the decider's answer. Before the answer becomes the step's code, the step checks the answer
against its own words, reading only plang's markers (a `%variable%`, a quoted literal, a number), never a human
language.

## Entry points

```
step.list Build (read the answers)                      goal/step/list/this.cs:164
├─ :185  step.Validate(check)       — the action chain judges itself      goal/step/this.Validate.cs:13
├─ :193  step.Cover(check)          — what the words hold that the answer doesn't, and back   this.Validate.cs:34
│        ├─ variables, both ways, compared by name as the variable list does
│        │   (variable.list.@this.Comparer: %Greeting% is %greeting%); a system variable (%!…) may be added
│        ├─ quoted literals the step writes (a doubled backslash told so)
│        ├─ numbers the step writes as digits, standing alone in the answer
│        └─ texts the answer writes that the step doesn't (invented)
├─ :196  step.Unwritten(check)      — numbers the answer writes that the words don't: the decider confirms
└─ :202  actions.Build(check)       — only code that judged itself sound
```

Each refusal is a line `step N ("text") — why`; a refused step's code is emptied so it is asked again.

## Reading a step from its .pr

`goal/step/serializer/Reader.cs` reads every key the step writes and refuses one it doesn't know
(`PrFormatOutdatedException`: another builder wrote it). One old key is passed over by name: `waitForExecution`, which
every older .pr carries — a step no longer says whether it waits; a goal call does (`goal.call`'s `Wait`).

## Tests

- `PLang.Tests/Runtime/App/SingularNamespaces/BuilderSchemaTests/MatchTests.cs` — a number the step writes and the
  answer drops; a quoted text with escapes; a variable written in another case is the step's; a dropped variable
  is refused.
- `ScopeTests.cs` — a variable the answer invents.

## Known faults

- Literals and texts compare as written (case-sensitive for literals, case-insensitive for invented texts):
  a literal is the programmer's exact text.
