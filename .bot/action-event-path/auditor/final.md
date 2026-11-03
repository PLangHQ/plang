# action-event-path — auditor v1

Verdict: PASS. Next bot: docs. Audited 950903427 against runtime2 4fb4d5043 (teaching only: on/event.{description,notes,examples}.md, pick_golden.json).

- Plan vs shipped: rule first in the notes, action examples varied (5 actions, comma and none), third+ added. Decision 4 ships 17/27 (Mark 3/5), short of done-when, with reasons (wording at its ceiling; the lever is structural, on the backlog). Agree it is a strict improvement; doc/events.md rightly keeps no output.
- Description: still words, no `%!…%` paths (names `file.read` only as an example action).
- Examples: each formal reads back (`%!app.module.<m>.<a>.on.start%`, When/Action well formed); the removed old examples are replaced by the varied ones.
- Teaching-edit rule: pick_golden re-pinned (state1 on-module e.g. lines only); Wire 656 green (647 + the tester's additions); other suites unchanged and cached green (Modules 1500, Types 1049, Data 882, Generator 191, Runtime 1513); `plang --test` 323/322 pass/0 fail/0 stale/1 skipped. Live llm gate and mutations not run.

Findings
- A1 (minor, deferred) notes.md: the new rule line gives `%!app.module.<module>.<action>.on.start%`, but the bullet below still gives `%!app.module.file.read%` (no `.on.start`). Two forms in one teaching; the writer may copy the short one. Align the bullet.
- A2 (note) the two committed fixture .prs with the old wrong path are ruled; agree.
