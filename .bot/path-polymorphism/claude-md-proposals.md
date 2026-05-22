## coder — v3 — 2026-05-22
**Target:** /CLAUDE.md
**Why:** v3 (codeanalyzer F3) made a value's boolean meaning its own
responsibility and turned the condition-evaluation pipeline async. Future module
and type authors need to know `Data.ToBoolean()` is no longer the place to add
type-specific truthiness, and that condition/assert evaluation is async — both
are easy to get wrong without a canonical note.
**Proposed change:**
Add under "## Runtime2 Conventions":

- **Truthiness — `IBooleanResolvable`**: a value's boolean meaning belongs to
  the value, not to `Data`. `Data.ToBoolean()` is the sync fallback (null/false/
  0/"" falsy, everything else truthy); do **not** add type-specific cases to it.
  A type that knows its own truthiness implements `app.data.IBooleanResolvable`
  (`Task<bool> AsBooleanAsync()`) — `path` does, where truthiness means "does
  the resource exist". `Data.ToBooleanAsync()` dispatches to it. Because that
  probe can be I/O (HTTP HEAD for the http scheme), the condition-evaluation
  pipeline is **async**: `IEvaluator.Evaluate` returns `Task<data.@this>`,
  `Operator.Evaluate` is `Func<data.@this?,data.@this?,Task<bool>>`, and
  `assert.IsTrue/IsFalse` are async. A new operator or evaluator must `await`.
