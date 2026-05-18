# docs — runtime2-foundation-verify

## v1 — 2026-05-18 — Test-only branch; one todos.md resolution marker filled

**What this branch is.** A foundation-verify branch: architect reconciled `Documentation/Runtime2/todos.md` against current code (three entries marked resolved, two new ones appended from depth-check findings), and coder shipped three `.test.goal` regression pins under `Tests/Errors/` for `error.handle` recovery-value semantics (handle.cs:109-114 GoalFirst, :120-131 RetryFirst symmetry, :177-184 chain ordering). Auditor PASS'd with one minor finding (deferred-with-consumer). Zero production C# changed.

**What docs did.**

Doc-gap scan came up nearly empty — no new modules, no new APIs, no user-visible behavior change. The only gap was a docs-hygiene one: the 2026-04-27 todo entry in `Documentation/Runtime2/todos.md` (the very todo this branch closed) lacked the `✅ RESOLVED` marker its peer entries had. Marked resolved with pointers to the three test files + handle.cs line refs + the auditor's minor caveat.

**Files modified:**
- `Documentation/Runtime2/todos.md` (line 66 header + body 67-75) — resolution marker + close-out paragraph, original entry archived in place.

**Files NOT modified (no drift, already accurate):**
- `Documentation/v0.2/good_to_know.md:176` — already documents `ErrorOrder=GoalFirst` "if recovery succeeds, skip retries" with handle.cs pointer.
- `Documentation/v0.2/debug.md:296-307` — already documents `%!callStack.Current.Handled%` and `%!error%` recovery-scope lifecycle.
- `Documentation/v0.2/callbacks.md:108` — error.App back-ref for recovery materialisation; unchanged.

**Code example — what the doc fix looks like:**

```markdown
## 2026-04-27 — PLang tests for error.handle recovery-value path  ✅ RESOLVED 2026-05-11 (`runtime2-foundation-verify` stage 6)

Three `.test.goal` regression pins landed in `Tests/Errors/`:
- `GoalFirstReturnsRecoveryValue.test.goal` pins `handle.cs:109-114` ...
- `RetryFirstReturnsRecoveryValue.test.goal` pins `handle.cs:120-131` — the 2026-04-27 symmetry fix.
- `MultiActionRecoveryLastActionPropagates.test.goal` pins `handle.cs:177-184` ...

### Original entry (archived)
[...original text preserved verbatim...]
```

Same shape as the archival/marker convention used by the 2026-04-24 lazy-generator and 2026-04-27 CallStack entries above it.

**Verdict.** PASS — ready to merge.
