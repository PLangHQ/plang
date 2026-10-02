# docs — friction

Format per entry: **what** · **cost (evidence)** · **wish**. Written also as a
dog-fooding review — where a doc's example didn't build or built differently than it
says, what a reader couldn't find, what I wished the language/builder did, and what
works well.

## Docs drift from code silently — nothing gates it
**What:** repeated cases where a doc stated something the code no longer does.
`object_pattern_formal.md` had five wrong examples (`app.Channel.Write`,
`Lifecycle.Before.Run`/`Step.Load`, `callStack.Error`, `app.type.type.@this`,
`A.Resize`); `file.md` said `list` returns "file objects with Size/Type/Exists"
(it returns `path` values) and `exists` "returns file info" (it returns the path);
`condition.md`/`loop.md` documented v0.1 names (`GoalIfTrue`, `GoalName`,
`ItemName`) and a `%position%`/`%listCount%` loop-variable table that v2 `foreach`
doesn't bind.
**Cost:** every one had to be hand-verified against the handler before I could fix
it; several (the `load vars` %! leak, the broken `foreach` example) were caught
only because the educator *ran* them. A reader trusting the old page would have
written code against members that don't exist.
**Wish:** the generate-from-source approach we built (pages rendered from the
catalog + a golden test) is the structural fix — drift becomes impossible. Extend
it to all modules and retire the hand-written `docs/modules/*.md`.

## Teaching examples that don't build
**What:** `os/system/modules/loop/foreach.examples.md` taught
`foreach %rows%, write out %row%` → at runtime `VariableNotFound: %row% is not set`
(without `as`, the element is `%item%`). The builder learns from that file, so it
also taught the model a failing form.
**Cost:** two fresh cache-off build cycles to pin it; a coder fix + a golden
re-render. **Build:** `/shared/educator/work/guide-examples/runs/loop-inline-*`.
**Wish:** a CI test that every `*.examples.md` `Step text:` line builds and runs —
teaching files are code, and a failing example teaches the builder a wrong pattern.

## Verifying against a stale checkout leads to wrong conclusions
**What:** I reported "`IsEvent` is still on app-systems" and nearly told the coder
their removal wasn't merged — my working copy was 76 commits behind origin. The
reverse also happened (a feature I thought stale was current).
**Cost:** a wrong message to a teammate; corrected only when they pushed back with
`git branch -r --contains`. Across the session, `app-systems` moved on almost every
push, so an FF push was rejected and re-rebased 1–3× per change.
**Wish:** a cheap "you are N commits behind origin/<branch>" signal before I verify
a claim, and a norm written down: always `git fetch` before concluding a
reviewer's finding is drift (it cuts both ways).

## A plang goal in docs/ can't write into os/
**What:** `Modules.goal` (in `docs/`) saving to `/system/modules/<m>/start.md`
writes into the app's own `system/` overlay (`docs/system/…`), not `os/system/`.
So the generator couldn't maintain the output page at the location Ingi chose.
**Cost:** blocked automation of the generated pages; needed decision 522 + a coder
rule (a new file at `/system/…` falls through to the os tree when the app has no
such folder). I had to place the three `start.md` by a direct filesystem copy as an
interim.
**Wish:** the filesystem-resolution rule (where a *read* vs a *write* to `/system/…`
lands, app overlay vs os) documented in one place a goal author can find — I only
learned it from the educator's probe + the architect's ruling.

## A reader can't find what properties a step has
**What:** the thread that started all this — a learner (and I) couldn't find, for a
step like `list files … matching … recursive`, which properties exist and how you
write each. That knowledge lived only in builder-facing `notes.md` mixed with
compile directives ("only when the step names one").
**Cost:** there was no learner-facing per-action reference at all.
**Wish:** (now built) the generated page's "How you say it" column + the per-module
guide. The `say:`/`builder:`/`ask:` two/three-audience split on one notes line is a
genuinely good design — one source, each reader sees its fields.

## Builder non-determinism on a simple option trigger
**What:** `read …, load vars` mapped to the `Template` option only ~1/4 fresh
builds for a while (then 5/6, then stable after issues 25/28); the dict `foreach …
with key` dropped its bindings 1/6.
**Cost:** a verified, correct example was unpublishable until the builder was
reliable — held the read worked example across several rounds.
**Wish:** determinism (or a confidence signal) for unambiguous option triggers, so a
learner copying a documented example gets the same compile every time.

## What works well
- The catalog exposing each element's `.Description`/`.Notes`/`.Examples` as lazy
  file items **plus** the attribute-derived property shape made page generation
  clean — one render, one golden, no re-parsing.
- `ModulePageTests` as a byte-exact golden gate caught **every** drift a downstream
  change introduced (the `Template` rename, the `{count, complete}` rename, the
  `foreach` example fix) — I re-ran it after each and it flagged or passed correctly.
- FF-only discipline (never `--force`) kept a fast-moving shared `app-systems` safe
  across dozens of pushes.

## Meta: CLAUDE.md itself is stale (friction for every bot)
**What:** the repo `CLAUDE.md` I operate under says plang tests live in `Tests/`
(it's `test/`), names `MarkdownTeaching.ScanOrphans` and
`PLang/app/module/MarkdownTeaching.cs` (both deleted), and lists path read-verbs
that were collapsed. A bot following it reaches for things that don't exist.
**Cost:** I only avoided the `Tests/` trap by `ls`-ing the repo; the `ScanOrphans`
reference sent the generator spec down a dead end until the fix bot traced it.
**Wish:** the backlog of CLAUDE.md proposals (`.bot/app-systems/claude-md-proposals.md`)
applied promptly — a stale canonical doc is the highest-leverage friction, because
every bot reads it first.
