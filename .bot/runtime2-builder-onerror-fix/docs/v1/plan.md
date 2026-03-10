# Docs v1 Plan — runtime2-builder-onerror-fix

## Context

This branch renames `RetryOverSeconds` to `RetryOverMs` end-to-end, strengthens the builder LLM prompt for onError preservation, adds 4 PLang test suites for error handling, and documents GoalFirst retry-skip behavior. All C# code already has correct XML doc comments (ErrorHandler.cs is well-documented). The code changes have passed codeanalyzer, tester, security, and auditor reviews.

## Stale Documentation Found

1. **`Skill/builder/builder-implementation-spec.md`** — ErrorHandler class (section 1.5) still says `RetryOverSeconds`. Missing new properties: `IgnoreError`, `Message`, `StatusCode`, `Key`. Example .pr file in section 13 also uses `retryOverSeconds`.
2. **`Skill/builder/builder-design-conversation.md`** — ErrorHandler type (section 9) still says `RetryOverSeconds`. Error handling examples in section 8 use "seconds" language.

## Documentation Gaps Found

3. **`Documentation/Runtime2/good_to_know.md`** — Missing: GoalFirst retry-skip behavior (when error goal succeeds, retries are skipped). This was a key finding during testing.
4. **`Documentation/Runtime2/todos.md`** — Row 7 "Retry testing" can be updated to reflect that retry tests now exist on this branch.

## Plan

1. Update `Skill/builder/builder-implementation-spec.md`: rename RetryOverSeconds → RetryOverMs, add missing ErrorHandler properties, fix .pr example.
2. Update `Skill/builder/builder-design-conversation.md`: rename RetryOverSeconds → RetryOverMs, add missing ErrorHandler properties.
3. Add GoalFirst retry-skip insight to `Documentation/Runtime2/good_to_know.md`.
4. Update retry testing status in `Documentation/Runtime2/todos.md`.
5. Write bot output files (summary.md, verdict.json, docs-report.json, report.json).
6. Commit and push.

## Not Needed

- XML doc comments: ErrorHandler.cs already has complete `///` docs. Methods.cs and GoalMapper.cs are existing code not modified by this branch (they were already present on runtime2).
- pr-file-format.md: Already uses `retryOverMs` — no update needed.
