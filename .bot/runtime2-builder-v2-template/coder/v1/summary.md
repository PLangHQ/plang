# UI Module (Template Rendering) — Coder v1 Summary

## What this is
PLang App UI module that renders Liquid templates via the Fluid library. One `render` action delegates to a swappable `ITemplateProvider` (default: `FluidProvider`). Templates access PLang memory stack variables by default, support explicit parameter overrides, can call PLang goals via `{% callGoal %}`, and include partials via `{% include %}`.

## What was done

### Production Code (3 files created, 2 modified)

**`PLang/App/modules/ui/render.cs`** — Action handler with `[IsNotNull] Template`, `List<Data>? Parameters`, `bool? IsFile`, `[Provider] ITemplateProvider`. One-liner `Run()` delegates to provider.

**`PLang/App/modules/ui/providers/ITemplateProvider.cs`** — Interface with single `Task<Data> Render(Render action)`.

**`PLang/App/modules/ui/providers/FluidProvider.cs`** — Default Liquid provider:
- Template resolution: `IsFile == true` → read file; `IsFile == false` → inline; `null` → auto-detect via `LooksLikeFilePath` heuristic
- Path resolution via existing `PathData` (goal-relative paths, engine-root absolute)
- Variable loading: `Variables.GetAll()` + explicit `Parameters` override
- Custom `callGoal` tag: `RegisterExpressionTag` — calls `engine.RunGoalAsync`, writes error inline on failure
- Include support: `PlangFileProvider` (nested class) adapts `IPLangFileSystem` → `Microsoft.Extensions.FileProviders.IFileProvider`; strips `.liquid` extension for compatibility
- HTML encoding via `HtmlEncoder.Default` for XSS prevention
- Error handling: TemplateError (400), NotFound (404), IOError (500), RenderError (500)

**`PLang/App/Engine/Providers/this.cs`** — Registered `ITemplateProvider`/`FluidProvider` + `"template"` type mapping.

**`PLang/PLang.csproj`** — Added `Fluid.Core` 2.31.0 (upgraded from initial 2.11.1 due to .NET 10 incompatibility with `include` tag parsing).

### Tests (29 C# + 5 PLang)

**C# tests** (`PLang.Tests/App/Modules/ui/RenderTests.cs`): All 29 implemented and passing. Covers inline/file rendering, variable resolution, explicit params, scoped var exclusion, callGoal tag, include partials, provider swap, path resolution, complex data types (dot nav, list iteration, null handling), HTML escaping.

**PLang test goals** (`Tests/App/Ui/`): 5 goals written with fixture template files. Not yet built (requires `plang p build --llmservice=openai`).

## Code example

```csharp
// render.cs — one-liner handler delegating to provider
[Action("render")]
public partial class Render : IContext
{
    [IsNotNull]
    public partial string Template { get; init; }
    public partial List<Data>? Parameters { get; init; }
    public partial bool? IsFile { get; init; }
    [Provider]
    public partial ITemplateProvider Provider { get; }
    public async Task<Data> Run() => await Provider.Render(this);
}
```

## Key issue resolved
Fluid 2.11.1 has a .NET 10 runtime compatibility bug: `FluidParser.TryParse` returns `false` for `{% include %}` tags with "Invalid 'include' tag" error. Upgrading to Fluid 2.31.0 resolved this completely. The bug did NOT manifest in standalone projects — only when loaded alongside the PLang assembly graph.
