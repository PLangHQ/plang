# docs v1 — filesystem-permission

## Inputs

- Coder output through v7; reviewer chain green
  (codeanalyzer v5, tester v6, security v2, auditor v2).
- `.bot/filesystem-permission/claude-md-proposals.md` — 2 proposals
  (architect v1 + tester v1).
- `.bot/filesystem-permission/character-proposals.md` — not present
  (no character proposals on this branch).
- `auditor-report.json` — one finding `missed_by: codeanalyzer` —
  doc-comment inversion in `PLang/app/actor/permission/this.cs:11-13`.

## Decision per CLAUDE.md proposal

### architect v1 → `Documentation/v0.2/good_to_know.md`

**Decision: apply.**

Reason: the existing OBP Smell Checklist catches "collection-without-owner"
shapes (smells 1–4) but doesn't name the **helper-function smell** —
free functions that take a domain object and return a derived answer.
This pattern is genuinely tempting when sketching in C#-shaped
pseudo-code and is canonical (applies to all future OBP work, not just
this branch). The proposed text is concrete, has a worked example in
the file's existing style, and includes a litmus test. Apply verbatim
(modulo numbering — current list has 4 items, this becomes #5).

### tester v1 → `CLAUDE.md` (root)

**Decision: apply.**

Reason: real incident on the branch (Ingi was alarmed by silent
mutation-test edits). The rule is canonical (applies to every future
reviewer/tester pass), the cost of the announcement is one sentence,
and the technique itself is legitimate. Reviewer-bot exception is
explicitly invoked and stated in the proposal footnote. Apply
verbatim as a new section "## Mutation Testing (announce first)"
after the existing "## Running plang Tests" section.

## Decision per character proposal

None present. No action.

## Doc gaps to fill

### G1. `PLang/app/actor/permission/this.cs:8-18` — XML doc inversion

The class-header summary lists Session as "no expiry on signature" and
Persisted as "signature has an expiry". Post-F-A the polarity is the
opposite: persisted "a" grants are signed with `Expires == null`
(permanent — they are the long-lived ones), session "y" grants are
unsigned. The F-A remediation patched the equivalent comment in
`filesystem/permission/this.cs:22-26` but missed this sibling.
**Action:** rewrite lines 8-18 in-place to match implementation.

### G2. `Documentation/v0.2/app-tree.md` — Actor surface missing `Permission`

The "Actor surface" block lists `Name`, `App`, `CancellationToken`,
`Context`, `Variables`, `Channels`, `Identity`, `FreezeFoundational()`
but not `Permission`. The branch added `actor.Permission` as a public
property on Actor and it is now a load-bearing part of the runtime.
**Action:** add one line under Actor → `Permission → app/actor/permission/`
with a Find/Add/Revoke summary, plus a maintenance row.

### G3. `Documentation/v0.2/filesystem-permission.md` — new architecture doc

The filesystem permission system is a major new concept with no
canonical doc home. `path-polymorphism-plan.md` mentions it tangentially
in the context of the next branch. The Permission **shape** (Verb,
PermissionRecord, two grant homes, Authorize flow, SkipFreshnessCheck
rationale) needs to be documented so future bots and developers can
reason about it without re-reading 4 files.

**Action:** write `Documentation/v0.2/filesystem-permission.md` covering:
- The Authorize flow (`path.Authorize(verb)` → in-root short-circuit →
  `Find` existing → prompt y/n/a → sign + store).
- The two-homes shape: in-memory (unsigned, session, dies with App) vs.
  sqlite-backed (signed, persisted, survives `new app()`).
- Identity = (Actor + Path + Verb); per-actor sqlite filter.
- The Ed25519 grant-verification flow and `SkipFreshnessCheck`
  rationale (steps 2 + 4 skipped; steps 1, 3, 5, 6, 7, 8 still run;
  step 8 = core signature gate is intact). Cite security v2's
  4-step bypass-scoping template.
- Known follow-ups: auditor F-C `RootComparison` not threaded
  everywhere, `Add` dedup-by-Path overwriting different-verb grants.

Aim: 150–200 lines. Reader audience: PLang developer reading C# and
debugging a permission denial.

## Out of scope

- CHANGELOG — no user-visible PLang-level changes documented in the
  repo today (no CHANGELOG.md at root); the .goal language surface is
  unchanged for users on this branch.
- PLang user-facing docs (`Documentation/v0.2/cool.md` style) — no
  new `.goal` syntax landed.
- `Documentation/Runtime2/good_to_know.md` — the project CLAUDE.md
  mentions writing PLang-architecture insights there. The OBP smell
  goes in `Documentation/v0.2/good_to_know.md` per the proposal's
  explicit target.
- `path-polymorphism-plan.md` — that document is the **handoff plan**
  for the next branch and is correct as written; it stays as-is.

## Order of operations

1. Apply architect proposal (good_to_know.md).
2. Apply tester proposal (CLAUDE.md).
3. Fix G1 (actor/permission XML doc).
4. Fix G2 (app-tree.md Actor surface).
5. Write G3 (new architecture doc).
6. Write `docs-report.json`, `verdict.json`, `summary.md`,
   update `report.json` actions+after, commit, push.

## Verdict outlook

PASS. The branch is reviewer-green; this docs pass closes the one
residual doc bug (G1) and brings docs current with the new system.
