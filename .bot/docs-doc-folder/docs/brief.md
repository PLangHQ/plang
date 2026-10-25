# Brief: build `doc/`, a new documentation folder

For the docs bot that implements it. Branch `docs/doc-folder` (off runtime2, which has `bugs` merged). Ingi's direction, in his words: "this needs to be new doc folder, we can learn from the documentation folder" and "your job will be to write totally new doc, you must verify code with expectations".

## Why

PLang is unknown, so the docs' first job is to bring a stranger from "what is this" to "I ran something" to "I trust it". A cold reader (a fresh agent given only `README.md` and `docs/`) could not finish. Findings, each checked against source:

- `README.md` links to `Documentation/README.md`, which does not exist.
- `docs/` tells a stranger to run `plang exec`. `exec` is no command: the bare word is read as the goal's name (`PLang/app/start/this.cs:37`).
- `docs/` says a first run builds. Run mode never builds; an unbuilt goal answers `GoalNotBuilt` (404) "isn't built; run plang build" (`PLang/app/goal/list/this.cs:63`). The real path is `plang build`, then `plang`.
- `plang --version` (install's "Verify") has no handler I found.
- Nothing says what a failed build looks like or what to change.
- `docs/getting-started/` has not changed since 2026-03-01.

Bots are readers too (`CLAUDE.md` and the teaching markdown already are a doc set), and bots are the only cold readers we can run.

## What is decided

- A new folder `doc/`. `Documentation/` and `docs/` are source material to read. Do not edit them, the generated module and type pages (`os/system/modules/**`, `os/system/type/**`), any teaching markdown, or CLAUDE.md in this work.
- First reader: the stranger. First slice: `start.md` (what PLang is, one screen), `install.md`, `first-goal.md` (write `Start.goal`, build, run), `when-a-step-fails.md`.
- One fact, one home. A page names one reader and the one question it answers.
- Docs state what IS: no history, no task or branch references.
- No hard-wrapped markdown: one paragraph or bullet is one line.

## The rule that makes it trustworthy

Every claim is an expectation. Before a page says it, run it (build the CLI with `./dev.sh build`, run `PlangConsole/bin/Debug/net10.0/plang` in a scratch folder) or read it at its source line. Record each in `verify.md` beside the page: the claim, how it was checked (command and output, or `file:line`), and pass / fail / unverified. A claim that fails is not written as true. The code or the claim is wrong; report the difference to the architect and leave the page saying only what held. What needs a live LLM key or a payment prompt and cannot be run is marked `unverified` on the page, never guessed.

## Acceptance

Run a cold reader again: a fresh agent given only `doc/` (not `Documentation/`, `docs/`, CLAUDE.md or source) must install, write and run a goal that reads a JSON file and prints one field, and say what to do when a step fails to build, with no guessing. Report where it stuck. Fix the page, not the reader.

## Still open (Ingi has not decided; do not build ahead of it)

- Which other sets exist after the stranger's: contributor, auditor or regulatory, a place for the *why* (decision records), a scratch `raw/`. Each is a reader with a question; decide from who really asks, after this slice is read cold.
- Whether `doc/` replaces `docs/` (the website's source, with generated module pages), and when `Documentation/` gets a "superseded by" note.
- The cites to `Documentation/v0.2/...` in CLAUDE.md, comments and generated pages are changed in one pass at the end, not piecemeal.

## Working notes

A plan and a first-slice draft of the plan are at `.bot/docs-doc-folder/docs/v1/plan.md`. Commit and push the branch; report to the architect in the subject / Done / HASH / CHECK / OPEN shape. Merge only when the architect relays Ingi's go.
