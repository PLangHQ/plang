## architect — event-action — 2026-10-29
**Target:** /CLAUDE.md (Runtime2 Conventions)
**Why:** Ingi, reading `goal/this.cs:405–414` (`if (result.Returned) { … } return result;`): "we should always try to return early, so `if (!result.Returned) return result;` … (this is also for code analyzer, so send him)". Not in CLAUDE.md or the conventions today.
**Proposed change:**
```
- **Return early.** A guard that ends a method returns first; the rest of the body is never nested inside an `if`. Write `if (!result.Returned) return result;` then the work, never `if (result.Returned) { … } return result;`. The codeanalyzer flags the nested form in every file a branch touches.
```
