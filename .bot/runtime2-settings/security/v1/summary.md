# Security Audit v1 — runtime2-settings

## What this is

Security review of the new Settings infrastructure added to PLang Runtime2. The feature provides strongly-typed, goal-scoped module configuration (~200 LOC production code) with a scope chain resolution pattern.

## What was done

**Blue team**: Mapped 4 attack surface areas — settings resolution, settings write, security control weakening (gzip bomb limit now configurable), and module view.

**Red team**: Attempted exploitation of identified vectors. Found 4 findings, all low/medium severity, all accepted-risk under user-sovereign threat model.

### Findings

| # | Severity | Category | Vector | Status |
|---|----------|----------|--------|--------|
| 1 | Medium | Resource exhaustion | Unbounded Scope dictionary growth | Accepted risk |
| 2 | Low | Resource exhaustion | Parent chain O(n) traversal (iterative, not recursive) | Accepted risk |
| 3 | Low | Info disclosure | Bare catch in Cast<T> swallows critical exceptions | Accepted risk |
| 4 | Low | Injection | archive.Max settable to 0/negative, disabling gzip bomb protection | Accepted risk |

### Key Files Reviewed
- `Engine/Settings/this.cs` — Resolve<T>, Set, Cast<T>, For<T>
- `Engine/Settings/Scope.cs` — ConcurrentDictionary wrapper
- `Engine/Settings/ModuleView.cs` — Context-bound view
- `Engine/Context/PLangContext.cs` — SettingsScope property, Clone
- `Engine/Goals/Goal/Methods.cs` — Save/null/restore pattern
- `actions/archive/Settings.cs` — First use case

### Architecture Assessment

The Settings system is clean:
- **Proper scope isolation**: Goals save/null/restore SettingsScope in finally block
- **Thread-safe**: ConcurrentDictionary throughout
- **Defensive type conversion**: Silent fallback to class defaults
- **OBP-compliant**: Navigate through engine.Settings, behavior on owner
- **No new trust boundary crossings**: All inputs from trusted .pr files

### Future Watch

If settings ever become writable from untrusted sources (transport-received Data, external APIs), two things need hardening:
1. Value validation (min/max bounds on security-critical settings like Max)
2. Dictionary size limits on Scope

Currently not a concern because the write path is only reachable from .pr file actions.
