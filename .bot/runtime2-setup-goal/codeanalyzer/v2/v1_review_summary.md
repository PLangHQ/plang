# v1 Review Summary

Code analyzer v1 found 3 issues, all addressed by coder v2:

1. **Finding 1 (High): Failed setup steps permanently marked as executed** — Fixed. Steps.RunAsync now only records on success or tolerated error. Test `RunAsync_FailedStepNotRecorded` added.
2. **Finding 2 (Medium): Setup.Record silently swallows errors** — Fixed. Record now returns `Task<Data>`.
3. **Finding 3 (Low): Count/All include setup goals but Get excludes them** — Fixed. Added `AllIncludingSetup` (internal), filtered `All`/`Count`/`Value`.
