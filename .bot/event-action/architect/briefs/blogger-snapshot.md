blog post: snapshot (Ingi asks for a draft, not a seed)
Start
- git -C /workspace/plang-ref fetch, then read at origin/runtime2 (6cfe782d7, snapshot's merge, or later):
  - .bot/snapshot/architect/plan/start.md — why snapshot exists and Ingi's decisions
  - Documentation/v0.2/snapshots.md
  - os/system/modules/snapshot/*.md — what save and resume do, as plang teaches it
  - test/plan/snapshot/ — start.md and the tests, the plang that proves it
- draft one post at ideas/2026-10-29-snapshot.md, Status: drafted. Its spine: a run's exact state, saved to disk or a db and resumed later (Ingi: "everything. this is not for web requests, we save this to disk or db and run later"). Then the plang steps a developer writes (save, write it to a file, read it back in a fresh process, resume), what comes back as it was (the variables, the call frames, the events a program bound, the settings), and the trust around it: signed by the app, written in its own view, a [Sensitive] value never in it, an edited snapshot resumes only once signed again, a changed goal resumes at its place with a warning
- quote real plang steps and real paths from plang-ref; no invented API. When the docs and the code disagree, the code wins and you note it in Open questions
- commit and push to blog-ideas main (message: Idea: snapshot), and add it to README.md's index
LIMITS: nothing written to PLang; only what the code and docs say today
DONE WHEN: the draft is pushed
REPLY: the path and the commit
