# v7 Review Summary

Tester verdict: **PASS** (approved). C# 2760/2760 green.

Two minor missing-coverage findings on `PLang/App/Channels/Channel/Events/this.cs`:

1. **F1 — B1 (`_active` instance scope) has no regression test.** Reverting `instance → static` keeps the suite green (tester verified empirically). Suggestion: instantiate two `Events.@this`, call `evA.Enter("X")`, assert `evB.IsActive("X") == false`.

2. **F2 — L1 (copy-on-write Enter) has no regression test.** A naive `Task.WhenAll` test is a false green; tester self-corrected after empirical validation. Suggestion: pause a child mid-Enter via `TaskCompletionSource`, observe the parent flow's `IsActive` on the child's id while the child is still inside its scope. Without L1, the parent flow sees the child's id leaked through the shared `HashSet` reference.

Non-blocking, but tester recommends a small follow-up commit (≤25 lines) before security.
