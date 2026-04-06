# UI Module (Template Rendering) — Coder v1 Plan

## Task
Implement the UI module for PLang App: Liquid template rendering via Fluid library.

## Files Created
- `PLang/App/modules/ui/render.cs` — render action handler
- `PLang/App/modules/ui/providers/ITemplateProvider.cs` — provider interface
- `PLang/App/modules/ui/providers/FluidProvider.cs` — default Liquid provider

## Files Modified
- `PLang/App/Engine/Providers/this.cs` — register ITemplateProvider + type mapping
- `PLang/PLang.csproj` — add Fluid.Core 2.31.0
- `PLang.Tests/App/Modules/ui/RenderTests.cs` — implement 29 C# tests
- `Tests/App/Ui/` — implement 5 PLang test goals + fixture files

## Key Decisions
- `IsFile` property (bool?) for explicit file/inline control
- `RegisterExpressionTag` for callGoal (supports both quoted and unquoted goal names)
- `PlangFileProvider` bridges IPLangFileSystem to Microsoft.Extensions.FileProviders.IFileProvider
- `PathData` reused for path resolution
- Fluid 2.31.0 required (2.11.1 has .NET 10 compatibility bug with include tag parsing)
- HTML encoding on by default via HtmlEncoder.Default
- ResolveType mapping: "template" (not "ui")
