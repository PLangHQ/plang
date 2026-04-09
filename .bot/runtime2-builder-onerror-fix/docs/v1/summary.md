# Docs v1 Summary — runtime2-builder-onerror-fix

## What this is

Documentation update for the RetryOverSeconds-to-RetryOverMs rename and builder onError fix. Two stale spec docs referenced the old property name and were missing new ErrorHandler properties. Architecture docs needed a GoalFirst retry-skip insight.

## What was done

**4 files updated:**

1. **`Skill/builder/builder-implementation-spec.md`** — Renamed `RetryOverSeconds` to `RetryOverMs` in ErrorHandler class definition (section 1.5) and shared types example (section 2.4). Added 4 new ErrorHandler properties: `IgnoreError`, `Message`, `StatusCode`, `Key`.

2. **`Skill/builder/builder-design-conversation.md`** — Renamed `RetryOverSeconds` to `RetryOverMs` in ErrorHandler type definition (section 9) and .pr file JSON example (section 13). Added same 4 new properties. PLang step examples kept natural language ("over 10 seconds") since the LLM converts to ms.

3. **`Documentation/App/good_to_know.md`** — Added "GoalFirst Retry Behavior" section documenting that when ErrorOrder is GoalFirst, successful error goal execution skips retries entirely. This was a key finding during testing (coder v3).

4. **`Documentation/App/todos.md`** — Updated retry testing row from NOT STARTED to DONE, referencing ErrorRetryOnly and ErrorGoalFirst test suites.

**Not needed:**
- XML doc comments: ErrorHandler.cs already has complete `///` docs from the coder.
- pr-file-format.md: Already uses `retryOverMs`.
- New PLang examples: Not the docs bot's job; tester already created 4 test suites.

## Code example

Before (builder-implementation-spec.md):
```csharp
public int? RetryOverSeconds { get; set; }
```

After:
```csharp
public int? RetryOverMs { get; set; }
public bool IgnoreError { get; set; }
public string? Message { get; set; }
public int? StatusCode { get; set; }
public string? Key { get; set; }
```
