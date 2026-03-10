# v2 Summary: Rebuild .pr files properly with plang builder

## What this is
Reverted manual .pr file edits from v1 and properly rebuilt all .pr files using the plang builder. Manual .pr editing is never allowed in PLang — only the builder generates .pr files.

## What was done

1. **Reverted manual .pr edits** — `git checkout HEAD~2 -- system/builder/.build/` restored the original builder output
2. **Rebuilt system/builder** — Deleted stale per-step `BuildGoal/` folder and ran `plang p build --llmservice=openai`. The builder regenerated into v0.2 single-file format (`buildgoal.pr` instead of per-step folders).
3. **Rebuilt test .pr files** — Built ErrorRetryOnly and ErrorGoalFirst test suites from scratch.
4. **Verified retryOverMs** — Confirmed:
   - `ErrorRetryOnly/.build/errorretryonly.test.pr` has `retryOverMs: 500` (correct)
   - No `retryOverSeconds` appears anywhere in rebuilt .pr files
   - system/builder/.build/ uses v0.2 format without embedded schema (scheme is read from .goal at runtime)

## Key files modified
- `system/builder/.build/` — Old per-step BuildGoal/ folder replaced by v0.2 single-file `buildgoal.pr`
- `system/builder/.build/build.pr`, `applystep.pr`, `buildstep.pr`, `app.pr` — Rebuilt
- `Tests/Runtime2/ErrorRetryOnly/.build/` — New .pr files from builder
- `Tests/Runtime2/ErrorGoalFirst/.build/` — New .pr files from builder

## What the reviewer flagged (v1)
v1 manually edited .pr files instead of rebuilding. This is a hard rule violation — .pr files are builder output only. Fix: revert and rebuild.
