# Piece 6: UI Module — Template Rendering

## Decision Log

- **No separate template module.** Template rendering is a capability of the UI module. One module eliminates builder confusion about which module to route to.
- **Liquid via Fluid.** Replaces Scriban. Liquid syntax works both server-side (C#/Fluid) and client-side (JS). Provider-based — Fluid is the default, Scriban or others can be swapped in.
- **Template syntax stays `{{ }}`**, not `%var%`. Clear boundary: `%var%` = PLang resolves at step time, `{{ var }}` = template engine resolves at render time.
- **Memory stack accessible by default.** All PLang variables are available in templates without explicit passing. Explicit params (`with name=%user.name%`) create aliases/overrides.
- **One `render` action.** No separate `renderFile` — the handler detects whether input is a file path or inline content.
- **UI-specific actions (DOM, layout, navigation) are NOT in scope.** This piece covers template rendering only. DOM/layout/events are a future piece.

## Overview

The UI module renders Liquid templates with access to PLang's memory stack. Templates can call PLang goals (`callGoal`) and include other templates (`render`). The Fluid library provides the Liquid implementation, swappable via the provider pattern.

## Provider Interface

```csharp
public interface ITemplateProvider : IProvider
{
    Task<Data> Render(Render action);
}
```

Methods accept the action record (OBP). Returns `Data` — rendered string on success, error on failure.

## Default Provider: FluidProvider

Uses [Fluid](https://github.com/sebastienros/fluid) (OrchardCore's Liquid engine).

**Responsibilities:**
- Parse template string (Fluid `FluidParser`)
- Build `TemplateContext` with variables from memory stack + explicit params
- Register custom filters/tags: `callGoal`, `render`
- Execute `RenderAsync` and return result

**Custom tags/filters:**

| Name | Signature | Description |
|------|-----------|-------------|
| `callGoal` | `{% callGoal 'GoalName' data %}` | Calls a PLang goal, returns `Data.Value` or throws on error |
| `render` | `{% render 'path/to/partial.html' %}` | Renders another template inline (partial/include), inherits variables |

**Variable loading:**
1. Load all memory stack variables (skip `!`-prefixed scoped vars)
2. Load current call frame variables
3. Apply explicit params from action (these override if same name)

**Notes on `date_format` and `json`:** Fluid's built-in Liquid filters include `date` (formatting) and `json` (serialization). No custom implementations needed — these were Scriban limitations.

## Action

### render

```csharp
[Example("render 'email.html', write to %body%", "Template=email.html")]
[Example("render 'page.html' with title=%pageTitle%, write to %html%", "Template=page.html, Parameters={title: %pageTitle%}")]
[Example("render %templateContent%, write to %result%", "Template=%templateContent%")]
[Action("render")]
public partial class Render : IContext
{
    [IsNotNull]
    public partial string Template { get; init; }

    public partial List<Data>? Parameters { get; init; }

    [Provider]
    public partial ITemplateProvider Provider { get; }

    public async Task<Data> Run() => await Provider.Render(this);
}
```

**Template resolution in the provider:**
1. If `Template` value is a path (file exists on disk) → read file content, render
2. Otherwise → treat as inline template content, render directly

**Path resolution:** Relative to the calling goal's directory. If the goal is at `/user/GetUser.goal` and template is `template/user.html`, it resolves to `/user/template/user.html`. Leading `/` means app root.

## `callGoal` Implementation

The `callGoal` tag needs engine access to run a goal. The provider gets this through the action record → `action.Context` → engine.

```
{% callGoal 'ProcessItem' item %}
```

1. Resolve goal by name from current engine
2. Build parameters from tag arguments
3. `await engine.RunGoal(goalCall)`
4. Return `Data.Value` (inserted into template output)
5. On error → throw (Fluid catches and reports with line/column)

## `render` (Partial Include)

```
{% render 'components/header.html' %}
{% render 'card.html' item=product %}
```

1. Resolve path relative to the current template's directory (not the goal's directory — important for nested includes)
2. Read file content
3. Render with inherited variables + any explicit params
4. Insert result into parent template output

**Note:** Fluid has built-in `{% render %}` and `{% include %}` tags. We should use Fluid's native `{% include %}` for partials (it inherits the parent scope) and register `callGoal` as a custom tag. Check if Fluid's built-in `render`/`include` can be configured with our file system and variable scope — if yes, no custom implementation needed for partials.

## Module Structure

```
PLang/App/modules/ui/
├── render.cs                          — render action handler
├── providers/
│   ├── ITemplateProvider.cs           — provider interface
│   └── FluidProvider.cs               — default Liquid implementation (Fluid)
```

## NuGet Dependencies

- `Fluid.Core` — Liquid template engine

## Registration

In `Engine/Providers/this.cs`:
```csharp
Register<ITemplateProvider>(new FluidProvider());
```

In `ResolveProviderType()`:
```csharp
"ui" or "itemplateprovider" => typeof(ITemplateProvider),
```

## Test Expectations

### C# Unit Tests (~10)

- render: inline template with variable substitution
- render: file template reads and renders
- render: missing file returns error
- render: explicit params override memory stack variables
- render: memory stack variables accessible without explicit passing
- render: `callGoal` executes goal and inserts result
- render: `callGoal` with error returns error data
- render: nested `render`/`include` works
- render: path resolution relative to goal directory
- render: Liquid syntax error returns error with line/column info
- provider: swapped provider is used

### PLang Integration Tests (~5)

- Render file template with variables
- Render inline content
- Render with explicit params
- Render with `callGoal`
- Render with nested include

## Files to Create

| File | Purpose |
|------|---------|
| `PLang/App/modules/ui/render.cs` | Render action handler |
| `PLang/App/modules/ui/providers/ITemplateProvider.cs` | Provider interface |
| `PLang/App/modules/ui/providers/FluidProvider.cs` | Default Liquid provider (Fluid) |

## Files to Modify

| File | Change |
|------|--------|
| `PLang/App/Providers/this.cs` | Register `ITemplateProvider` + type mapping |
| `PLang/App/GlobalUsings.cs` | Add UI module alias if needed |

## Definition of Done

- `render` action works with both file paths and inline content
- Memory stack variables accessible in templates by default
- Explicit params create aliases/overrides
- `callGoal` custom tag calls PLang goals from templates
- Partial includes work (via Fluid built-in or custom)
- Path resolution relative to calling goal's directory
- Provider is swappable via settings
- Fluid (Liquid) is the default provider
- C# and PLang tests pass
