# v1 Summary — ISettings Design

## What this is

A general-purpose settings mechanism for PLang Runtime2. The trigger was a hardcoded 100MB gzip bomb limit in `Data.Envelope.cs`, but the design is universal — any module can declare strongly typed, goal-scoped settings that PLang developers configure with natural language steps like `- set max gzip size to 20mb`.

## What was done

Architectural design session. No code written — this is a coder handoff spec.

**Key design decisions:**

1. **Per-module ownership (OBP)** — each module owns its settings. Archive module has `ArchiveSettings`, not a central settings service. Navigation: `engine.Module<Archive>().Settings.Max`.

2. **Strongly typed, no strings** — `Module<T>()` is type-keyed, settings properties are real C# types. No `Libraries["archive"]` string indexing.

3. **Source generator does everything** — developer writes one partial class implementing `ISettings`. Generator produces: scope-aware property bodies (read), settings action handler (write), and registry (builder discovery).

4. **Goal-scoped with default override** — settings set in a goal inherit to subgoals, reset when goal completes. `Default` flag persists at engine level. Resolution: current scope → parent → engine default → class default.

5. **Context-bound view for thread safety** — `Module<T>()` returns a lightweight wrapper stamped with current context, so concurrent goals don't share settings state.

## Code example

What the developer writes:
```csharp
public partial class ArchiveSettings : ISettings
{
    public long Max { get; set; } = 100 * 1024 * 1024;
}
```

What the source generator produces (read side):
```csharp
public long Max => _context?.SettingsScope.Resolve<long>("archive.max") ?? 100 * 1024 * 1024;
```

What consuming code looks like:
```csharp
// In Data.Decompress()
var max = _context.Engine.Module<Archive>().Settings.Max;
```

## Status

Design complete. Ready for coder handoff. Builder integration (discovery action, LLM prompt design) deferred to a later phase.

## Files

- `v1/plan.md` — full design spec with phases, scoping rules, source generator details, and open questions
