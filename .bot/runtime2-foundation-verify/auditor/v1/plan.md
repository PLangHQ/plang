# Auditor plan — runtime2-foundation-verify v1

## What I'm auditing

Branch landed two commits on top of `runtime2`:
1. **architect** — todos.md reconciliation + foundation depth-check (`verification.md`) + stage-6 brief (no code).
2. **coder** — three `.test.goal` files under `Tests/Errors/` pinning `error.handle` recovery-value semantics.

No codeanalyzer / tester / security reports exist. I am the only reviewer.

## Approach

Pure docs/test branch, narrow blast radius. Strategy:

1. Verify diff scope — branch only touches docs + new tests + .pr artifacts. No production C# changes.
2. Re-read `PLang/App/modules/error/handle.cs` lines 90-185 and confirm the three pinned behaviors map to real code branches.
3. Re-read each `.test.goal` and the matching `.pr` — confirm the builder produced the modifier shape the brief asked for (Order=GoalFirst / Order=RetryFirst+Retry=1 / 3 sequential variable.sets).
4. Run a clean `dotnet build PlangConsole` + `plang --test` from `Tests/` to bypass the stale-binary trap and confirm the three new tests pass alongside the rest.
5. Look for what reviewers usually miss on test-only branches:
   - Test that ASSERTS a different thing than the brief intended.
   - Test that PASSES for the wrong reason (the side-effect lands regardless of the branch the pin is supposed to lock).
   - Architectural fit (placement, naming, idiom).
   - Pollution of `Tests/Errors/` namespace with files that don't belong to the recovery-value family.

## What I will NOT redo

- Architect's verification.md depth-check on Snapshots / Identity / Settings / KeepAlive. That's their work; I read it for context only.
- Builder behavior. Coder verified .pr shapes; I'll spot-check one.
