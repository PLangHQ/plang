# codeanalyzer — decider-key, review 1 (493b157df → 8786cc08d)

**Verdict: FAIL** — one small finding (F1, a missing pin). Design and code are clean. Coder next, then a quick re-check.

## F1: the ask's error as `Missing`'s cause is not pinned (deletion test)
`IKey.cs:33` `if (asked.Error is { } error) missing.list.Add(error);` is the boy-scout change (architect 2: the ask's error rides whole as the cause, not glued into the message). Announced mutation: line removed → MissingDeciderKeyTests 3/3, MissingKeyTests 3/3, DeciderProviderTests 2/2 all stay green. Reverted. The new tests assert the key, the message and CallCount only. Add one assertion in NoOneToAsk (and MissingKeyTests' no-input case): `decided.Error.list` holds the ask's error (NoOneToAsk or whatever `output.ask` answers with no input). A coder test, one line.

## Read whole, smells run by name — clean
- `IKey.cs`: one asking for two settings, in the module that owns `output.ask`, the core untouched (architect 2). The `(setting.@this)this` cast is accepted there; not re-raised. `Missing` builds a fresh error per read, so adding a cause doesn't mutate a shared one.
- `llm/setting/this.cs`, `decider.setting.cs`: each states `Question` and `Missing` (explicit members), `Asked` left the llm setting (its callers are the two doors, both cast to `IKey`). Live's dead `IsNullOrEmpty` header guard is gone (an empty key already ended in MissingLlmKey).
- `TypeSafe.cs`, `Live.cs`: the key stays a `Data<text>` to the Authorization header (Ingi's ask); TypeSafe asks before the body, so no key, no request (CallCount 0 in the test). A failed key answers whole through `From(key)`.
- No locked surface touched; no new OBP smell.

## Measured
- Bench (`BENCH_SKIP="build birth request"`; the diff is llm code only): `plang --test` 4.5 s (297 pass, 1 fail, the baseline's); C# sweep 40.6 s on the second run (68.9 s on the first, at load 13: noise), red only Wire's.
- Green: MissingDeciderKeyTests 3, MissingKeyTests 3, DeciderProviderTests 2.
- Mutation (announced, reverted, tree clean): the cause dropped → nothing red (F1).
