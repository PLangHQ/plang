# doc2 v1 plan

Write the first slice of `doc/` from `.bot/docs-doc-folder/docs/brief.md`: `start.md`, `install.md`, `first-goal.md`, `when-a-step-fails.md`, and `verify.md`. Each claim is run or read at its source line, and recorded in `doc/verify.md` as pass, fail or unverified.

Blocker found while verifying: the builder's decider calls a separate service that needs `TYPESAFE_API_KEY`; none is available, so the build half is unverified. The pages say so.
