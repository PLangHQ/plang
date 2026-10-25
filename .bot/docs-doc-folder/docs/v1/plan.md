# doc/ — a new documentation folder

## Purpose

`doc/` is written new, not moved. `Documentation/` and `docs/` are source material to read; nothing in them is edited. A page enters `doc/` only when each claim in it has been checked against the running code.

## First reader

A stranger who has never heard of PLang. Their path: what it is, install, a first goal, a build that fails and what to do. Their cold read of the current docs (a fresh agent, README and docs only) found a dead front link, commands that do not exist, and no failure path.

## The rule for every page

Each claim is an expectation. Before it is written it is run, or read at its source line, and the result is recorded in `verify.md` beside the page: the claim, how it was checked (command and output, or file:line), and pass / fail / unverified. A claim that fails is not written as true: the code is wrong or the claim is, and the difference goes to the architect.

## Pages (first slice)

1. `start.md` — what PLang is, in one screen, and where to go next.
2. `install.md` — get a `plang` on the path, and prove it runs.
3. `first-goal.md` — write `Start.goal`, build, run.
4. `when-a-step-fails.md` — what a failed build looks like and what to change.

Later slices, decided after this one is read by a cold reader: the other readers (contributor, auditor, bots) and the set of folders they need.

## Not in this slice

No edits to `Documentation/`, `docs/`, the generated module or type pages, any teaching markdown, or CLAUDE.md.
