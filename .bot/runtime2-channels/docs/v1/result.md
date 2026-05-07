# docs v1 result — runtime2-channels

**Verdict:** PASS — ready to merge.

## What this branch shipped (CHANGELOG-equivalent)

`runtime2-channels` introduces channels as the per-actor I/O primitive, replacing the old `App.IO` shape and purging `Console.*` from production C#. Highlights:

- **Per-actor `Channels` registry** (`App.Channels.@this`). Each Actor (System / Service / User) owns its own. Pure registry — Register / Remove / Get / Resolve. Choreography (writes, reads, serializer routing) lives on `Channel.@this` and its subtypes.
- **Channel subtypes** under `App.Channels.Channel.{Stream,Goal,Session,Events,Message}.@this`. Stream is the dominant subtype; backs stdin/stdout/stderr, files, memory buffers, HTTP bodies.
- **Standard role-channels** — names `output`, `error`, `input`, `debug`. Defaults are wired by the entry point (PlangConsole) before user code runs; `App.Run` enforces that every actor performing I/O has all three.
- **Redirectability** — the whole point. Tests register memory channels; users can re-register `debug` to a file; future Stage 9 transport will register over HTTP/WebSocket.
- **`Debug.Write(...)`** as the single diagnostic surface. Resolves the System actor's `debug` channel, falling back to `error`. Gated on `IsEnabled` so it goes silent without `--debug`.
- **Console.\* purge** (v10) — every production write site routed through channels. Documented as a canonical rule in `/CLAUDE.md` and `Documentation/v0.2/good_to_know.md`.
- **Channel.Migrate / MigrationEnvelope deletion** (v9) — the prototyped cross-device migration surface was removed before merge after security review flagged a misleading signature shape and the receive side wasn't designed. Resurrection happens fresh under Stage 9 transport. `Documentation/Runtime2/cool.md` updated to read clearly as roadmap.
- **`StreamReader` regression test** (v9 A3 fix) — bounded read with `ResolveEncoding` and `leaveOpen: true` for the prompt path.

## What I changed in this version

| File | What | Why |
|---|---|---|
| `Documentation/Runtime2/cool.md` | Reframed the channel-migration entry as roadmap; explicit note that the v0.x surface was removed pending Stage 9 | Auditor v2 N1 — present-tense "the channel.migrate action snapshots…" was misleading after v9 deletion |
| `Documentation/v0.2/good_to_know.md` | Added "Console.\* Is Banned in Production C#" section | v10 establishes a non-obvious canonical rule (3 write flavours + 2 explicit exemptions); needed a reference long-form |
| `/CLAUDE.md` (Runtime2 Conventions) | One-line bullet pointing at the long-form rule | Canonical guidance for future bots working in `PLang/` |

## What I flagged but did NOT fix

- **`Documentation/v0.2/io-channels.md` is materially stale.** It predates the OBP restructure and per-actor split: names types like `App.IO.IO` / `App.IO.Channel` / `actor.IO`; current code is `App.Channels.@this` per actor (`actor.Channels`) with `Channel.{Stream,Goal,Session,Events,Message}.@this` subtypes. The factory list (`Channel.Input/Output/Memory/File`) doesn't match the current API either. **Severity: major.** **Resolution: flagged-for-coder/architect** — the right shape for a fix is its own architect-pass for a v0.2 channels reference doc, not a fix-up inside this docs gate. Calling this branch "merge-blocked on a stale unrelated doc" is the wrong tradeoff.
- **Auditor N2 / N3** — Stage 9 carry-forward notes (don't resurrect the deleted surface; build the permission gate on Variables-snapshot exposure into the new design from the start). Nothing actionable on this branch; live in `cool.md` already after the rewrite.

## Findings summary

| Severity | Category | Resolution |
|---|---|---|
| minor | stale-doc (cool.md migration entry) | filled |
| minor | missing-doc (Console.* discipline rule) | filled (good_to_know.md + CLAUDE.md) |
| major | stale-doc (io-channels.md) | flagged-for-coder/architect |

No critical findings. No PLang user-facing examples needed (no new modules / actions added on v10 — only routing changes).
