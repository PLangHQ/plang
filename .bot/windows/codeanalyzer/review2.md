# codeanalyzer — windows, review 2 (1610440fc → 728280d24)

**Verdict: PASS** — F1 and both notes fixed; the tester is next.

- **F1 (0d9b61d6e):** the probe's summary states the fact, no slice or red state.
- **N1 (8e867a32b):** `Hop.IsLookup` (virtual, true), `Method` answers false, `code.IsLookup` asks all hops; the `__Live` guard asks only `Code.IsLookup: true`. The owner of a hop says whether it only looks up: no type-switch in the generator. Announced mutation (`Method.IsLookup => true`): `ARequiredVariableThatCallsAMethod_RunsItOnce` red; reverted, RequiredVariableTests 4/4.
- **N2 (5873cc903):** one `Comparison` for exact and glob: a file's grant by the OS's disk, any other scheme's as written. `ParseScheme` of `C:\x` is "" (no `://`), so a Windows path stays a file grant. GrantMatchTests 8/8 on Linux; `AUrlGrant_KeepsCase` and `OnWindows_AGlobGrant_IgnoresCase` can only go red on Windows, so the host's run pins them.
- **728280d24:** `Typed.Combine` no longer trims twice; its summary says its child is never rooted.

Production changes are three small members and the guard's condition; the bench of review 1 stands (no new work on the birth or request path beyond an `All` over a variable's hops). Tree clean.
