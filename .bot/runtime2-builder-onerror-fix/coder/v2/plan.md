# v2 Plan: Revert manual .pr edits and rebuild with plang builder

## Problem
v1 manually edited .pr files (retryOverSeconds -> retryOverMs) which violates the rule that .pr files are builder output only.

## Tasks
1. Revert manual .pr edits using `git checkout HEAD~2 -- system/builder/.build/`
2. Rebuild system/builder with `plang p build --llmservice=openai`
3. Rebuild test .pr files for ErrorRetryOnly and ErrorGoalFirst
4. Verify rebuilt .pr files use retryOverMs (not retryOverSeconds)
5. Commit and push

## Approach
- No manual .pr edits - only the plang builder generates .pr files
- OpenAI API key is now available in the environment
