# docs — item-doors v6 — plan

Brief: `.bot/item-doors/architect/briefs/docs.md`. Docs only: no production C#, no `.pr`, no teaching `.md` (so the teaching-edit rule is not triggered; the generated `os/system/modules/file/start.md` says nothing about what prompts guard).

## Edits

| # | Item | File | What |
|---|---|---|---|
| 1 | what a prompt guards (315d) | `Documentation/v0.2/filesystem-permission.md` | new section "What the prompts guard": the User actor's doing, not the app's own code (System by choice, own channels) |
| 2 | `.data/` (317) | `filesystem-permission.md` | one line in the stores' folder section |
| 3 | how the gate judges a link (319b/c) | `filesystem-permission.md` | new section "Where a verb is judged"; Authorize flow rewritten to the place the door hands the gate |
| 4 | a signed value that arrives (319a/c) | `Documentation/v0.2/callbacks.md` (+ pointer in `wire-serialization.md`) | "A signed value that arrives": one nonce presentation per arrival, verify/name signer any number of times, replay = same bytes again → NonceReplay |
| 5 | H, bin to a tty (315d/214) | `Documentation/v0.2/io-channels.md` | one line on `%!app.channel.setting.format%` |
| 6 | N18 stale names | `bans.md`, `app-tree.md`, `filesystem-permission.md`, `debug.md`, `scripts/check-app-tree.sh`, `os.targets` (comment), `cli-changes.md` (same stale flag forms; added) | `app.filesystem.root` / `.os`; `--call.setting`, `--debug.setting…` |
| 7 | CLAUDE.md | `CLAUDE.md` | proposals + N18 lines (decisions below) |
| 8 | good_to_know | `Documentation/v0.2/good_to_know.md` | index entries for the rules from 300–319 |

## CLAUDE.md proposal decisions

1. creative name (architect v1, 2026-10-18) — **apply** (a rule for every bot; Shape smells; attribution trimmed, docs state what is).
2. One door for writing (107/119/124) — **reject**: withdrawn by the next entry; teaches a shape that no longer exists.
3. A value always writes itself (167–202) — **apply**, replacing "A value writes what it is; the writer decides how it looks". Checked: `item.Output(writer) => writer.Write(this)` non-virtual (`item/this.cs:849`), per-writer `Write` overloads (`:861–919`), `View.Store/Out/Debug`, `IWriter.Context`.
4. lowercase narrowing (202) — **apply**. Checked: members read ignoring case (`item/this.cs:272`).
5. Debugging flags (coder 214/215/218) — **apply**, with N18's Debugging line. Checked: `ExecutorTests.cs:300–319`.
6. `IContent.Content` → `content.Content(context)` (222a) — **apply**. Checked: `IContent` gone; `content/this.cs:103`; `item.Plain` (`item/this.cs:775`, `binary/this.cs:159`).
7. N18 `CLAUDE.md:11,27` — **apply**: the anchors are `app.filesystem.root` / `app.filesystem.os` paths.

## Left out
- `identity.export` teaching (N1), N1–N17, R3/198, `backlog.md`: not in this pass.
- `CLAUDE.md` still lists `Data.Merge()` (no such member): fixed on `docs/file-module-guide`, not here.
