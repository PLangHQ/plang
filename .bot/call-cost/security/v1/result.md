# call-cost — security v1 (b8c0d4ac9, against runtime2 7bf093245)

Verdict: PASS. No finding at Low or above. Three Info notes.

Patterns: 11 (the first ask loads, and what is kept stays honest by its version), 12 (one answer on the element: the keeper).

## What I checked, by the architect's six items

1. **Kept option answers (`actor/setting/this.cs` `Get(action, option)`).** Kept at the keeper (the nearest scope with a run value or an actor's rows), keyed (module, action, option), valid at the root's `_version`, read *before* the answer is worked out, so a write during the work leaves the entry stale and never wrong. Every Set, Save, Remove and the rows' read moves the version (`Read`'s failure path returns without a Move; the answer is then NotFound as before, the rows never retried, same as runtime2). The user's keeper is the user's setting, the system's its own: the user's never answers for the system's, the system's value answers for the user's through the chain (probe S1, both warm). A sub app has its own actors and its own root, so its version and answers are its own (S2: the sub's system actor and the maker's, both orders). A child layer with a value is its own keeper and its descendants read through it, a sibling and parent never (S3). Tester S1–S5 cover set/clear, child, actors, sub app, absent parameter.
2. **Resolver.** `Resolve` is an instance method that reads only its `context` argument and `action`; no use of `this`/`Context`/`__ctx` (read `Emission/Action/this.cs` EmitResolve, EmitAttach). The run's handler is born by the private ctor with the run's context (`new X(context, …)`). R1: a run in a sub app writes the sub's context only, whoever warmed the resolver first, the maker's user and system and the sub's system see nothing; the resolver's own context is never a run's. R2: a parameter variable reads in the run's variables (maker "maker", sub "sub", system's "system" never read). The resolver is `internal`, not tagged for plang.
3. **Types.** `Kinded`/`Made` key the kept type by (entry, name, kind, strict, template name); the bare entry is returned untouched for no kind (T1). A kind a sub app coins on a type it shares with its maker is the maker's object too: the documented cache ("a kind coined once"), the same as before the branch (`Made` coined before).
4. **`context.Json`.** The reader holds only `_context`; depth is a parameter (cap 128), nothing is kept across reads. A runtime-ingest read (ctx null) still gets the plain entry type, never a template (`Kinded(null, null)` is the entry).
5. **Generated `Resolve` cascade.** Same order and the same predicates as before: seed, the step's row (`HasValue`: a given null or 0 is given), the setting, the frozen default, `[Default]`/Uninitialized. The row is now read once and first (`__d`); the fallback for a non-nullable slot with no default is `__d ?? NotFound`, as `__View` answered. No change in which of the step, a setting or a default answers, so identity, signing and path actions are unchanged.
6. **`goal.list.Find`.** The kept `found` is made and let go under the gate `Add` takes, and `Add` is the only writer of the list's goals (grep: no Remove/Replace/Clear). Per list: a goal added to the maker's list moves neither the sub's list nor its kept candidates, and the other way (F1). A caller of another app is answered in its own app first (line 206), so a kept answer is never an app's answer for another's caller.

## Info

- **I1.** A kept answer is one `Data` shared by every run of its keeper (before: made fresh per ask). A run value was already shared; a saved row's `Option(...)` is now one Data born with the keeper's context, not the asking layer's. Only a saved row whose value holds a `%variable%` would read it in the keeper's variables instead of the asking goal's. I found no such row shape (rows are saved values), did not probe it.
- **I2.** A C# action's constructor now runs once, with the module's app's System context, instead of once per run with the run's. A third-party handler (module.add, an accepted trust) whose constructor reads its context gets System's. Built-in handlers' constructors only assign fallbacks.
- **I3.** The probe's Make.Goal writes the shared test disk, so a sub app's `Find` of the maker's goal name can open it from disk in a test. Not a production finding.

## Probes

`PLang.Tests/Runtime/App/Context/CallCostSecurityProbeTests.cs`: 7 green (S1–S3, R1, R2, T1, F1). No production edit, no mutation test.
