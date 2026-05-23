# Coder — v4 plan

## Task

Merge `origin/runtime2` into `path-polymorphism`. The plang builder was fixed on
runtime2; this branch needs those builder-quality wins.

## Approach

1. Fast-forward local `path-polymorphism` to `origin/path-polymorphism`
   (picks up codeanalyzer v2 review).
2. `git merge origin/runtime2`. Resolve conflicts.
3. Build, run both test suites, confirm no regression vs v3 baseline
   (C# 2881 pass; PLang 202 pass / 0 fail / 1 stale).

## Conflicts

- `PLang/app/modules/builder/code/Default.cs` — one conflict. runtime2 added a
  path-resolution fix (`rootRelative`: prefix `/` so the builder anchors at the
  user's cwd, not its own tree). This branch renamed `filesystem.path` → `path`.
  Resolution: keep runtime2's `rootRelative` fix logic, use this branch's `path`
  type — `path.Resolve(rootRelative, context)`.

## Design divergence (decided with Ingi)

runtime2's builder-quality commit (8166e753b) re-added a try/catch *inside* the
generated action handler, around `Run()` — wraps bare CLR exceptions (NRE,
InvalidCast) as `ServiceError` carrying `{module}.{action}: {ExType}: {msg}`.
This contradicts this branch's "Phase 3" thin-handler invariant asserted by two
generator tests (`GeneratedExecuteAsync_HasNoTryCatchFinally`,
`GeneratedExecuteAsync_CallsRunDirectly`).

Decision (Ingi): **keep runtime2's wrap** — strictly better error messages,
catches NRE that `Call.ExecuteAsync` deliberately excludes. The two stale tests
are updated to match the new contract (try/catch around `Run()`, no `finally`).
