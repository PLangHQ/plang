# Births pass — a value loads with the context of the Data that asks

Designed with Ingi 2026-09-23. **Released to coder 2026-09-23.** Supersedes the "values read the running context / App as birth fact" version (step B, never committed — dropped).

> **You (coder) own this.** The rules are settled with Ingi. Shapes, names not fixed below, and the commit split are yours. Code below is direction.

## Why

The program (`.pr`) is loaded once, outside any run, and every run of every actor reads the same parameter rows. Today runs write onto those rows and use contexts stored on them:

- The generator stamps the run's context onto the shared row (`PLang.Generators/Emission/Action/this.cs:370-377`), and the stamp reaches the shared value. Two actors running one goal overwrite each other — proven: System's handler read User's `%x%`.
- Without the stamp, a literal keeps the loader's context — proven: a literal path checked as System when User ran it.
- `goal.call` stamps the callee's context onto its own program rows and puts the shared row into the callee's variables (`goal/call.cs:112-113`).

Values made during a run are not the problem (Ingi): they live in that actor's memory, and reach another actor only as a parameter.

## The design

**1. Each run gets its own Data for each parameter, born with the run's context — the generator does it.** Step 1 as you built it: `action[name]` selects the row (replaces `GetParameter`), the generator gives plain slots `Copy(context)` and typed slots `As<T>(context)`, and never writes on the row. `ShallowClone` → `Copy`; `As<T>(answer)` internal. Handlers only touch generated properties, so there is nothing to forget. Any other run-time code that reads a row directly takes its own copy the same way — list the ones you find.

**2. `.Value()` loads with the context of the Data that asks, not the value's own.** `Value(data)` already receives the asking Data. `source` uses its stored context instead (`source.cs:117` `Get(Context)`, `:138` `Load(_value, Context)`, and `Read()`); it uses `data.Context`. Same rule for every value door that loads or renders: the `list`/`dict` templates (a `%x%` element loads with the asking Data's context, not the list's), `text`. The path template already does it (`path/this.cs:144`). One rule, no exceptions.

```
action.Run(User)
 → generator: Path = the run's own Data, born with User's context
 → handler: await Path.Value()
    → the row holds "file.txt", not loaded yet
    → loads with data.Context = User — User's permission check, cached only on User's Data
```

**3. The row holds its literal unloaded.** If the `.pr` reader (or anything at load/build) leaves a loaded value on a row — e.g. a `path` item, whose literal `Value(data)` returns itself with the loader's context (`path/this.cs:142`) — rule 2 can't reach it. Verify what rows hold after a `.pr` read; a loaded context-aware value on a row is a bug to fix at that point.

**4. `goal.call` arguments: each call gets its own Data per argument, born with the CALLER's context** (Ingi). No write on the shared row; the callee's variables get the copy. `%city%` in `call Foo city=%city%, actor=system` loads from the caller's memory even when System reads it; a path the caller passes is loaded with the caller's permissions. Arguments stay unresolved until read — no fork for cross-actor calls.

**Step A (`26823c1d1`, the slot on the App) is not needed for this.** It stays; whether it stays for good is decided at the type-object step (values born with no context in hand).

## Tests (red first where not red already)

1. The six `SharedProgramTests` with your corrected premises.
2. The six regressions step 1 caused alone go green: `VariableHoldingAName_Selects`, `Foreach_Cancellation_StopsIteration`, `Foreach_ClrJsonPlanSteps_BindsStepWithIndex`, `Render_CallGoal_SuccessWritesValueToOutput`, `Run_StrictImageGifWithRuntimeVarResolvingToPng_ThrowsTypedError`, `VarAsImageGifStrict_BuildsClean_FailsAtRuntime`.
3. **Concurrent:** one goal run by System and User at the same time; each sees its own `%x%` and its own permission checks.
4. **Literal file, two actors:** User holds the read grant, the other actor doesn't → denied, not served from a cache; the file changes between runs → the next run reads the new content.
5. **Cross-actor goal.call:** `call Foo city=%city%, actor=system` — the callee reads the caller's `city`; the program row is unchanged after the call.
6. `Data_GenericAsT_DoesNotExistAsPublicApi` green (`As<T>(answer)` internal).

Six suites by name; nothing red committed.

## Demolition — this step

- `__ResolveData`'s stamp on the shared row; `GetParameter`; `ShallowClone` (→ `Copy`).
- `goal.call`'s `arg.Context = execContext` and binding the shared row into the callee's variables.
- Every value door that loads or renders with its own stored context instead of the asking Data's.

**Stays:** values keep the context they were born with (the App, the registry); `Context.Ok(x)` and the other context births; explicit `context` parameters; step A's slot (for now).

## After this step (unchanged goals, re-planned when step 1 lands)

- The type object is the registry's own (`Promote()`, the static fallback and primitive statics die; the menu gets choice values, #27) — decides the slot.
- The remaining post-birth stamps and `_context = null!` (born-with, no late stamp).
- #15 (`ReturnTypeName`), #24 (`test.Create` static).

## OBP validation

| Surface | Check |
|---|---|
| run's parameter Data | born per run with the run's context; the program is read-only for runs |
| `Value(data)` | the ask carries the context; the value doesn't load with a stored one — one rule for every value |
| `action[name]` | the owner selects its parameter; no verb+noun |
| `Copy()` / `Clone()` | new wrapper over the same value / deep copy |
| goal.call argument | a copy born with the caller's context; no write on the shared program |
