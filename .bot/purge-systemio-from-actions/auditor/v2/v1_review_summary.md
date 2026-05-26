# Summary of auditor v1 review

**Verdict:** FAIL — 1 MAJOR finding.

**F1 — review-gap.** Tester v2 reported PLang `--test` as 206/206. Clean
rebuild from blank `bin/obj` reproduced 204/206. Two failures in
`Builder/CompileLlmNotes/` (`output-write-no-channel.test.goal`,
`assert-equals-no-message.test.goal`), both with `Channel 'input' has
no interactive answerer (stream EOF)` — the signature of an AuthGate
prompt firing against the test runner's non-interactive channel.
Bisect: commit `064724fda` (F1 canonicalization) introduced the
regression. Runtime2 baseline was 206/206; pre-F1 was 206/206.

The failing first-step in both tests:
```
- copy file '../../Simple/.build/start.pr' to 'start-out.txt', overwrite
```

Pre-F1, the un-canonicalized `..` segments textually prefix-matched
root and `IsInRoot` auto-granted. Post-F1, canonicalization at the
FilePath ctor resolved `..` first — the resolved absolute fell outside
the test child app's scope and escalated to a permission prompt.

F1 itself was correct (closes a HIGH-severity path-traversal vuln);
my finding was that the test-side rooting needed to align with
canonical semantics, not that F1 should be rolled back.

**Side check:** PLNG002 suppression audit — zero `pragma warning disable
PLNG`, zero `SuppressMessage(...,"PLNG002")`, zero `NoWarn` containing
PLNG002, zero `.editorconfig` overrides. Two file-scope carve-outs
(`IsPathHelperFile`, `IsPathTypeSurface`) inside `Plng002.cs` only —
confirmed per Ingi's explicit ask.
