# Review of v1 — Builder Module Test Stubs

Fresh-eyes review identified 6 gaps in the v1 test suite:

1. **No test for corrupt/invalid .pr JSON** — `GetGoals` merges existing `.pr` data, but nothing tests malformed `.pr` files. Real-world scenario when builder crashes mid-write.

2. **Missing GoalFile line number tracking** — Steps have `LineNumber` but no test explicitly asserts correct 1-based line numbers from source.

3. **Missing PrPath derivation test** — `Parse_PathComputation` mentions PrPath but doesn't have a dedicated test for the `.goal` → `.build/*.pr` mapping formula.

4. **Building guard test was singular** — One vague test for "all builder actions" doesn't give the coder clear signals. Each of the 8 actions needs its own guard test.

5. **PLang test stubs too thin** — Just `throw "not implemented"` with a comment. Coder has to guess the PLang syntax for calling builder actions. Need concrete step shapes.

6. **engine.RunAction I/O pattern** — Noted but deferred. This is an implementation detail that's hard to test at stub level — the coder will need to verify the pattern themselves.
