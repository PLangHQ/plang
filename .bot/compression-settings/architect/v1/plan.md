# ISettings — Strongly Typed, Goal-Scoped Module Settings

## Problem

`Data.Envelope.cs` (on `data-envelope-architecture` branch) hardcodes a 100MB gzip bomb limit:

```csharp
private const long MaxDecompressedSize = 100 * 1024 * 1024;
```

This should be configurable by the PLang developer:

```
- set max gzip size to 20mb
```

But there's no settings mechanism in Runtime2. This design introduces one — general-purpose, not gzip-specific.

## Design

### Core Pattern

Each module that needs configuration declares a **strongly typed settings class** implementing `ISettings`. The source generator handles everything else — scope-aware property resolution, settings action handler generation, and registry.

The developer writes:

```csharp
public partial class ArchiveSettings : ISettings
{
    public long Max { get; set; } = 100 * 1024 * 1024;
    public CompressionLevel Level { get; set; } = CompressionLevel.Optimal;
}
```

That's it. The source generator does the rest.

### Navigation

Strongly typed, no strings:

```csharp
engine.Module<Archive>().Settings.Max
```

From any code with context (e.g., Data.Decompress):

```csharp
var max = _context.Engine.Module<Archive>().Settings.Max;
```

### Scoping

Settings are **goal-scoped** — set in a goal, inherited by subgoals, reset when the initiating goal completes.

```
Goal: ReceivePayload
- set max gzip size to 20mb         // scoped here
- decompress %data%                 // uses 20mb
- call ValidatePayload              // subgoal inherits 20mb

Goal: ValidatePayload
- decompress %nested%               // still 20mb (inherited)

Goal: SomeOtherGoal
- decompress %stuff%                // default 100mb
```

**Default** scope persists at engine level:

```
Goal: Setup
- set default max gzip size to 20mb // engine-level, survives across goals
```

Resolution order: current goal scope → parent goal scope → ... → engine default → baked-in class default.

### What the Source Generator Produces

From one `ISettings` partial class, the generator creates three things:

#### 1. Scope-aware property bodies (read side)

Rewrites each property to resolve from the context's settings scope chain, falling back to the declared default:

```csharp
// Generated
public long Max => _context?.SettingsScope.Resolve<long>("archive.max") ?? 100 * 1024 * 1024;
```

The caller sees a simple property. The scope resolution is invisible.

#### 2. Settings action handler (write side)

A strongly typed `settings` action for the module:

```csharp
// Generated in archive module namespace
[Action("settings")]
public partial record settings
{
    public partial long? Max { get; set; }
    public partial CompressionLevel? Level { get; set; }
    public partial bool Default { get; set; }  // goal-scoped (false) or engine-level (true)
}
```

Properties are **nullable** — only set properties are written to scope. The `Default` flag controls whether the write targets goal scope or engine scope.

The generated handler's Run method writes each non-null property to the appropriate scope.

#### 3. Settings manifest (for builder discovery)

Emits a registry of all `ISettings` types with their property names, types, and defaults. The builder uses this to give the LLM full schema context when mapping natural language to settings actions.

*(Details of manifest format deferred — figure out during builder integration.)*

### Runtime Flow

1. **PLang step:** `- set max gzip size to 20mb`
2. **Builder (LLM):** sees all settings schemas, maps to archive module's `Max` property, emits .pr:
   ```json
   { "module": "archive", "action": "settings", "parameters": [
       { "name": "max", "value": 20971520 }
   ]}
   ```
3. **Runtime:** generated settings handler executes, writes `20971520` to context's goal-scoped settings
4. **Later:** `engine.Module<Archive>().Settings.Max` resolves from scope → returns `20971520`

### Context Stamping

Settings objects need a context reference for scope resolution — same pattern as `Data._context` (late-bound).

**Thread safety concern:** The module is shared on the engine. Multiple goals may execute concurrently. The settings object can't hold a single `_context` on a shared instance.

**Recommended approach:** `Module<T>()` returns a lightweight context-bound view each time — just a wrapper holding the module reference + current context. Cheap (no allocation beyond the wrapper), thread-safe (each caller gets its own view). The coder should evaluate whether a struct wrapper or per-access instance is better.

### SettingsScope on PLangContext

Settings need their own scope mechanism on `PLangContext` — separate from `MemoryStack` (which is for user variables). Same push/pop-per-goal behavior:

- Goal runner pushes a settings scope at goal start
- Goal runner pops at goal end
- Engine holds the default scope (persistent)
- Resolution walks the stack: current → parent → ... → engine default → class default

## Implementation Phases

### Phase 1: Foundation
- `ISettings` interface
- `SettingsScope` on `PLangContext` (push/pop per goal, resolution chain)
- `Module<T>()` navigation on Engine (type-keyed lookup, context-bound view)

### Phase 2: Source Generator
- Detect `ISettings` classes
- Generate scope-aware property bodies
- Generate `settings` action handler per module
- Generate settings manifest/registry

### Phase 3: First Use Case
- `ArchiveSettings : ISettings` with `Max` property (default 100MB)
- Replace `MaxDecompressedSize` constant in `Data.Envelope.cs` with `_context.Engine.Module<Archive>().Settings.Max`
- PLang test: `- set max gzip size to 20mb` → decompress respects limit

### Phase 4: Builder Integration (deferred)
- Builder action to discover all settings schemas
- LLM prompt design for settings mapping
- "default" keyword handling

## Open Questions for Coder

1. **Context-bound view:** Struct wrapper vs class instance for the `Module<T>()` return? Struct avoids allocation but has copy semantics. Class is a small allocation per access. Profile and decide.

2. **SettingsScope data structure:** Simple `Dictionary<string, object>` per scope level with stack semantics? Or something more structured? The scope key format (`"archive.max"`) needs a convention.

3. **Push/pop integration:** Where exactly in the goal runner does scope push/pop happen? Same place as MemoryStack push/pop — needs to mirror that lifecycle.

4. **`Default` parameter naming:** `Default` is a C# keyword (in switch). May need `IsDefault` or `Persistent` or similar. Coder to decide based on what works with the source generator.

## Dependencies

- Builds on `data-envelope-architecture` branch (where `Data.Envelope.cs` and `Module<Archive>` exist)
- Source generator changes extend `PLang.Generators` (same project as LazyParamsGenerator)
- Does NOT affect .pr file format — settings are just another action in the .pr
