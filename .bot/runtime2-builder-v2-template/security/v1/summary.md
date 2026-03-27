# Security v1 Summary — UI Module (Template Rendering)

## What this is

Security analysis of the UI module added on branch `runtime2-builder-v2-template`. The module provides Liquid template rendering via the Fluid library, with a custom `callGoal` tag that executes PLang goals from within templates.

## What was done

**Blue team** — mapped the attack surface of FluidProvider, PlangFileProvider, module.remove, and event.on/skipAction. **Red team** — assessed SSTI, path traversal, and resource exhaustion vectors.

### Key findings

| # | Severity | Category | Status |
|---|----------|----------|--------|
| 1 | Medium | SSTI via callGoal | Accepted risk |
| 2 | Low | Resource exhaustion (no MaxSteps) | Open |
| 3 | Low | Info disclosure in errors | Accepted risk |
| 4 | Low | Type registration exposes properties | Accepted risk |
| 5 | Low | Properties.Clone() shallow | Accepted risk (carry-forward) |

### Carry-forward resolutions
- **DefaultEvaluator InvalidCastException** — FIXED (line 24)
- **Data.Envelope Decompress InvalidOperationException** — FIXED (line 160)
- **SqliteDataSource DeserializeValue** — No longer applicable (refactored away)

### Existing mitigations (good)
- HTML encoding by default (`HtmlEncoder.Default`)
- `ValidatePath` sandbox on all file operations including includes
- `!`-prefixed scoped variables excluded from template context
- `[Sensitive]` attribute on IdentityData.PrivateKey
- CallStack depth limit (1000) bounds callGoal recursion
- Event re-entry guard prevents infinite event loops

### Recommendation
**PASS** — no critical or high findings. The SSTI via callGoal (medium) is a developer responsibility issue, analogous to SQL injection when using string concatenation. Consider adding `TemplateOptions.MaxSteps` to Fluid configuration as defense-in-depth against resource exhaustion.

Suggest running the **auditor** next.

## Code example — the SSTI vector

```csharp
// FluidProvider.cs:181-218 — callGoal tag executes goals from template content
private static async ValueTask<Completion> CallGoalTagAsync(
    Expression expression, TextWriter writer, TextEncoder encoder, TemplateContext context)
{
    var engine = (Engine.@this)context.AmbientValues["engine"];
    var plangContext = (PLangContext)context.AmbientValues["context"];
    // ...
    var goalCall = new GoalCall { Name = goalName };
    var result = await engine.RunGoalAsync(goalCall, plangContext);  // <-- executes any goal
}
```

If a PLang developer writes `render %userInput%`, an attacker submitting `{% callGoal 'Setup' %}` gets goal execution. The fix is documentation + developer awareness, not a runtime change.
