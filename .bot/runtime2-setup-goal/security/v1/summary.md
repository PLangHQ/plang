# Security Audit v1 — runtime2-setup-goal

## What this is

Security audit of the Setup.goal run-once execution system introduced on `runtime2-setup-goal`. This branch adds a run-once mechanism for setup goals: steps are tracked by hash in the system DataSource, skipped on re-run, and re-executed when their hash changes (step content changed). Builds on top of the DataSource + Settings bridge from `runtime2-system-datasource`.

## What was done

**Phase 1 (Blue Team):** Mapped 8 attack surfaces — hash-based step tracking, IsTolerableError string matching, setup context propagation, record metadata (info disclosure), CallStack depth enforcement, DeserializeValue exception handling, executor integration, and settings bridge.

**Phase 2 (Red Team):** Attempted attack construction for each surface.

### Findings

| ID | Severity | Area | Issue |
|----|----------|------|-------|
| 1 | **Medium** | DeserializeValue | Catches `JsonException` but not `InvalidOperationException` from `UnwrapJsonElement` depth guard. Carry-forward from parent branch. |
| 2 | Low | IsTolerableError | Substring matching `Contains("already exists")` could false-positive on unrelated error messages. False positive = step tolerated + recorded, preventing re-run. |
| 3 | Low (accepted) | Setup.Record | Error messages persisted in system.sqlite may contain sensitive data from external systems. Same risk level as log files. |
| 4 | Low | SqliteDataSource | No use-after-dispose guard. Carry-forward. |
| 5 | Low | SqliteDataSource | EnsureTable on every op. Carry-forward. |
| 6 | By-design | SqliteDataSource | No app-level storage limits. User-sovereign. |
| 7 | By-design | Setup | Steps with null/empty Hash always re-run. |

### What's solid

- **Record-on-failure semantics correct:** Failed steps are NOT recorded — they re-run on next startup. Only success or tolerated errors get recorded.
- **Context propagation correct:** `context.Setup` set in `RunAsync()`, cleared in `finally`. Goals called during setup inherit run-once semantics. Normal execution (context.Setup == null) is unaffected.
- **CallStack depth enforced:** `CallStackOverflowException` at depth 1000, caught by `Step.Methods.cs:49` try/catch. Setup treats this as non-tolerable — step not recorded, setup aborts.
- **Hash tracking secure:** Hash comes from .pr file (trusted), stored via parameterized SQL. Null hashes safely handled (always re-run).
- **Setup goal exclusion correct:** `Get()` filters `!goal.IsSetup`. `AllIncludingSetup` is `internal`.

## Code example

The low-severity IsTolerableError finding — `Setup/this.cs:78`:

```csharp
// CURRENT: bare substring matching
public bool IsTolerableError(Data result)
{
    var message = result.Error?.Message;
    return message.Contains("already exists", StringComparison.OrdinalIgnoreCase)
        || message.Contains("duplicate column name", StringComparison.OrdinalIgnoreCase);
}

// POTENTIAL HARDENING: more specific patterns
public bool IsTolerableError(Data result)
{
    var message = result.Error?.Message;
    return Regex.IsMatch(message, @"\b(table|index)\b.*\balready exists\b", RegexOptions.IgnoreCase)
        || message.Contains("duplicate column name", StringComparison.OrdinalIgnoreCase);
}
```

## Verdict

**PASS** — no critical or high severity findings. The medium finding is a carry-forward from the parent branch (DeserializeValue exception gap). The two new low findings are specific to the setup system but have narrow conditions and low impact. Setup system security model is sound.
