# How a bot works here

For a person or a bot changing the runtime. The question this page answers: how is work organised here, what does each role do, and where do I leave my output?

Most work on the runtime is done by bots, each in a role, under the project owner's direction. A person works the same way: on a branch, with output beside the code, through the same gates.

## Roles

Each role has a folder `characters/<role>/` with a `character.md` that states the role (`characters/`). The ones that appear in the gate of a merge:

- **architect**: thinks, plans, and relays the owner's decisions. "Your job is not to write code. Your job is to think." (`characters/architect/character.md`).
- **test-designer**: turns the architect's design into test suites, C# and plang, that define the behavioural contract.
- **coder**: writes the C# for the runtime, following the shape rules (`characters/coder/character.md`).
- **codeanalyzer**: finds complexity and kills it.
- **tester**: checks that tests verify intent, not only implementation.
- **security**: thinks like an attacker, defends like an engineer.
- **auditor**: the second-opinion reviewer and cross-cutting integrity gate.
- **docs**: the documentation writer, "the last stop before merge" (`characters/docs/character.md`).
- Others serve a part of the system, for example **builder** (the goal files of the self-hosted builder).

A merge records its gate in its message. The merge of the decider-key branch (`b4a549d1c`) ends: "Line: codeanalyzer, tester, security, auditor PASS, docs satisfied."

## Branches

Work is on a named branch and is merged into `runtime2`. The merge commits on `runtime2` are named `merge <branch>`: `b4a549d1c` is `merge decider-key`, `14ef2a06a` is `merge docs/retire-docs`. A merge message records the owner's go in his words, for example `Ingi 2026-10-26: "1. yes"` (`b4a549d1c`). The architect relays that go.

## Output: `.bot/`

A role's output on a branch is in `.bot/<branch>/<role>/` and is committed with the work. `.bot/decider-key/` is an example:

```
.bot/decider-key/
  architect/   plan/, summary.md
  codeanalyzer/ v1/ (plan.md, verdict.json), v2/, review.md, summary.md
  tester/      v1/ (result.md, verdict.json)
  auditor/     verdict.json, final.md
  docs/        v1/ (plan.md, result.md, verdict.json)
  report.json  one record of every session on the branch
```

`report.json` is a list of sessions, each with `bot`, `intent`, `before`, `plan`, `actions`, `after` and the start and end times (a session's keys in `.bot/decider-key/report.json`).

## Proposals

Three kinds of change are proposals, not edits:

- **An addition to the locked core**: a short proposal to the architect, who brings it to the owner. Nothing is built before the yes ([the locked core](the-locked-core.md)).
- **A change to `CLAUDE.md` or a character file**: appended to `.bot/<branch>/claude-md-proposals.md` as `<author> — v<N> — <date>`, a target, why, and the exact text. The docs role picks them up and applies the ones that hold up (`CLAUDE.md`, "Proposing CLAUDE.md / character changes"). 31 branches in `.bot/` hold such a file.
- **A finding in code that is not yours**: report it with the line and the evidence to the architect. The code's owner fixes it; do not fix a locked part on your own judgement.

## Rules for a reviewer

A reviewer reads every file the branch touched, whole, not only the diff's lines, and marks an older finding with the commit it came from (`CLAUDE.md`, "The boy scout rule"). A file a branch touches is left better.

## When you work alone

The same steps apply to a person: cut a branch off `runtime2`, keep your output with your change, run the class you changed and `./dev.sh full` ([build and test](build-and-test.md)), and ask before a change to a locked part.
