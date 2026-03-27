# Code Analysis v1 Summary — UI Module + Clone Fixes

## What this is
Code analysis of the UI template rendering module (piece 6) and associated clone family fixes on `runtime2-builder-v2-template`. The UI module adds Liquid template rendering via Fluid, with a single `render` action that delegates to a pluggable `ITemplateProvider`.

## What was done
5-pass analysis on all files changed vs `runtime2-builder-v2-cleanup`. Analyzed:
- **3 new UI module files**: `render.cs`, `ITemplateProvider.cs`, `FluidProvider.cs`
- **5 clone family fix files**: `Data.cs`, `Properties.cs`, `PathData.cs`, `IdentityData types.cs`, `MemoryStack.cs`
- **3 other fixes**: `Data.Envelope.cs`, `DefaultEvaluator.cs`, `Providers/this.cs`

## Key findings

### Finding 1 (MAJOR): Clone() overrides drop Data metadata
`DataList.Clone()`, `PathData.Clone()`, and `IdentityData.Clone()` all copy subclass-specific fields but miss base `Data` fields: `Error`, `Handled`, `Warnings`, `Signature`, `Context`, `_type`. If any of these subclasses carries error state or signatures, cloning silently loses that information. This is ironic given the clone fixes were *the purpose* of this branch's non-UI changes.

**Pattern fix:**
```csharp
// PathData — before (missing Error, Handled, Warnings, Signature, Context)
public override Data Clone() => new PathData(_absolutePath, _fs, Value, Source)
{
    Name = Name,
    Properties = Properties.Clone()
};

// PathData — after (complete)
public override Data Clone() => new PathData(_absolutePath, _fs, Value, Source)
{
    Name = Name,
    Properties = Properties.Clone(),
    Error = Error,
    Handled = Handled,
    Warnings = Warnings != null ? new List<Info>(Warnings) : null,
    Signature = Signature,
};
```

### Finding 2 (MEDIUM): FluidProvider catch-all masks programming errors
`catch (Exception ex)` at lines 104 and 217 converts NullReferenceException and other programming bugs into user-visible error messages. Recurring pattern from previous reviews.

### Finding 3 (MINOR): Nested try/catch in PlangFileProvider.GetFileInfo
Double-nested exception handling is hard to follow. Should be simplified.

### Deletion test gaps
- `RegisterTypeIfNeeded`: no test uses a named class requiring Fluid type registration
- Successful callGoal: all tests use nonexistent goals — no test proves successful output injection

## Clean files
- `render.cs` — textbook thin handler
- `ITemplateProvider.cs` — correct OBP interface
- `Data.Envelope.cs` — InvalidOperationException catch appropriate
- `DefaultEvaluator.cs` — InvalidCastException catch appropriate
- `Providers/this.cs` — registration follows established pattern
- `Properties.cs` — Clone() clean

## Verdict: NEEDS WORK → send back to coder
