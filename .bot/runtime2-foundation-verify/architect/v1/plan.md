# Runtime2 Foundation Verify — v1

## Why this branch exists

When the user asked "what's next to bring runtime2 to runtime1 parity?", the first answer was based on `Documentation/Runtime2/todos.md` — and that file is dramatically stale. The CallStack wiring todo (2026-04-27) and the lazy generator OBP todo (2026-04-24) both describe work that has already been done on the cleanup branch. The actual code state and the todo file are out of sync.

That's a process bug worth fixing once, not a per-conversation correction: the next architect/bot to open `todos.md` will make the same mistake. So this branch does two things:

1. **Reconcile `todos.md` against the actual runtime2 state.** Walk every open entry, verify against code, mark resolved entries with a pointer to where they closed. Leave genuinely-open entries alone.
2. **Depth-check the four foundation areas not yet verified** — snapshots, identity, settings, keepalive — so we know what is actually solid before module-port work begins. Produce one `verification.md` report capturing what works, what's partial, what's gone the same way as the stale CallStack/generator entries.

This is the dependency in front of "start porting modules from main." Without it, the next module branch is built on the same uncertain ground that just produced a wrong plan.

## Scope

**In scope:**
- Audit every entry in `Documentation/Runtime2/todos.md`. Mark resolved ones with `✅ RESOLVED <date> (<branch>/<stage or commit>)` plus a one-line pointer.
- Depth-check Snapshot save/restore round-trip — does `App.Snapshot.Save` + `Restore` rehydrate an arbitrary running app, or only the slices currently covered by tests?
- Depth-check Identity bootstrap — does first-run identity creation work end-to-end? Recovery? Multi-identity?
- Depth-check Settings store — SQLite-backed, per-actor, write path. Is the surface complete or does it cut off somewhere mid-feature?
- Depth-check KeepAlive — daemon mode, long-running. Does it actually keep the process alive through real work?
- Carve a one-page test brief for item #7 (error.handle recovery-value tests) so test-designer/coder can knock it out without re-deriving the requirement.

**Out of scope:**
- Implementing fixes for any gaps the verification finds. If gaps surface, they get filed as new todos with current-state context and feed the next architect plan.
- The deferred items the user already settled (real symmetric crypto, fork-site Variables isolation, signing ratification, Events three-tier scoping, HTTP wire transport for ask-user). All of these are coupled to features that don't exist yet; documented as deferred but not re-opened.
- The CallStack parallel-execution scope todo (2026-05-08). Still legitimately open but only matters when WebserverModule lands. Will be revisited at that point.

## Stage index

| # | Slug | Status |
|---|------|--------|
| 1 | [todos-reconcile](stage-1-todos-reconcile.md) | pending |
| 2 | [snapshot-depth-check](stage-2-snapshot-depth-check.md) | pending |
| 3 | [identity-depth-check](stage-3-identity-depth-check.md) | pending |
| 4 | [settings-depth-check](stage-4-settings-depth-check.md) | pending |
| 5 | [keepalive-depth-check](stage-5-keepalive-depth-check.md) | pending |
| 6 | [error-handle-recovery-value-tests](stage-6-error-handle-recovery-value-tests.md) | pending |

Stages 2–5 each produce a short section in `verification.md` (not separate files) — keeping the report consolidated. Stage 6 is the test brief for the test-designer hand-off.

## Cross-cutting decisions

**What counts as "solid"** for a foundation area:
- Surface is present and used internally by at least one consumer.
- Tests exist (PLang `.test.goal` or C# TUnit) that exercise the full round-trip, not just the constructor.
- No `// TODO`, `throw new NotImplementedException`, or placeholder bodies in the public path.
- The behavior matches what `Documentation/v0.2/*.md` and `Documentation/v0.2/good_to_know.md` claim.

**What counts as a quiet gap:**
- Surface exists but is unused (like CallStack `Push` was, before it got wired).
- Tests cover the happy path but not the error path or restore path.
- Documentation describes a feature the code doesn't implement (e.g. "Settings stores per-actor" but only User actor is wired).
- A core method exists but throws on common inputs.

Quiet gaps get filed as new todos with the current-state context. They do *not* get fixed on this branch.

**Verification report format.** One `verification.md` at `.bot/runtime2-foundation-verify/architect/v1/`. Four sections (Snapshots, Identity, Settings, KeepAlive). Each section: "Surface", "Tests", "What works", "What's partial or missing", "Verdict (solid / partial / quiet gap)". Length target ~30–50 lines per section.

**Todos.md edit policy.** Conservative. Only mark `✅ RESOLVED` when the code change that closed the item is identifiable (commit hash or branch+stage). For entries where I'm not certain, leave them as-is and add a footnote saying "verified open as of 2026-05-11, owner=architect" so the next reader knows the file was reviewed.

## What ships when this branch closes

- `Documentation/Runtime2/todos.md` — audited and pruned.
- `.bot/runtime2-foundation-verify/architect/v1/verification.md` — the depth-check report.
- `.bot/runtime2-foundation-verify/architect/v1/stage-6-error-handle-recovery-value-tests.md` — test brief for test-designer/coder hand-off.
- `summary.md` — what was done, what each verdict says, what the next architect/branch should pick up.
- Possibly new entries appended to `todos.md` for any quiet gaps the depth-check surfaced.

After this lands, we have a credible answer to "is the foundation solid enough to start porting modules?" — yes or no, with the evidence.
