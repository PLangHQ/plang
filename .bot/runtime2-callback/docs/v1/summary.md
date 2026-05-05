# docs v1 — runtime2-callback

## What this is

Final docs gate for the callback subsystem branch. The branch added two callback impls (`AskCallback`, `ErrorCallback`), the `- run %callback%` verb with a strict seal-then-verify gate, an `application/plang+data` wire serializer, and an `ISnapshotted` pattern that lets App subsystems round-trip across a suspend. Auditor v2 PASS and security v1 PASS already cleared the code; my job was to make sure the architecture decisions and user-facing behaviour are documented, and to evaluate the four character/CLAUDE.md proposals filed on this branch.

## What was done

### Architecture docs
- `Documentation/v0.2/callbacks.md` (new) — the two ICallback impls, the seal-then-verify gate (S-F1), wire size caps (S-F3), sensitive-property strip (S-F4), the `%!ask.answer%` resume sentinel, position semantics, the lazy-Signature ICallback carve-out, the `application/plang+data` mimetype.
- `Documentation/v0.2/snapshots.md` (new) — ISnapshotted as the type-system classifier, the section tree shape, per-subsystem subtree reference (10 implementers), referent integrity (no silent fallback), why ErrorCallback's wire is narrower than its in-process snapshot.
- `Documentation/v0.2/architecture.md` — added Snapshot/Restore and Callbacks sections after Error Handling, each pointing into the topic file.
- `Documentation/v0.2/good_to_know.md` — appended three cross-cutting entries: the ICallback-only lazy-Signature carve-out (the trip-wires are subtle), RestoredFrame as surrogate (not pushable into AsyncLocal), and the Errors.Push App back-ref pattern.

### User-facing docs
- `docs/modules/callback.md` (new) — user-facing reference for `- run %callback%` with worked example, all five typed error keys, wire size limits, and how it relates to ask.
- `docs/modules/output.md` — rewrite: `ask` is now its own subsection covering `vars:` and the resume mechanism in plain terms.
- `docs/modules/index.md` — `callback` row added; `output` action list updated to include `ask`.

### Character proposals
All four proposals applied — they were branch-incident-driven, persistent, and aligned with their character roles:
- architect v2 (review server) + architect v3 (test-designer prep two-files) → `characters/architect/character.md`
- coder v4 (`.test.goal` stub contract) → `characters/coder/character.md`
- codeanalyzer v1 (read-only scope) → `characters/codeanalyzer/character.md`

### CHANGELOG
Drafted in `result.md`. The merge commit should pull from there.

## Code example — the docs pattern

The architecture doc explains the *decision* behind the seal-then-verify gate, not just the mechanism:

> The gate is the security boundary. Neither `AskCallback.Run` nor
> `ErrorCallback.Run` re-checks the signature. Adding new callback types
> means implementing `ICallback` and trusting this gate to vet the
> envelope before dispatch.

The user-facing doc explains the *guarantee* in plain terms:

> PLang signs and verifies the envelope for you — tampered or unsigned
> envelopes never resume.

Same fact, different audience. Architecture doc tells the next runtime developer where to extend. User-facing doc tells the .goal author what they can rely on.

## Verdict

**pass** — code is ready to merge.
