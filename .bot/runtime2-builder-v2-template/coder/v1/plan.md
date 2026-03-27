# UI Module (Template Rendering) — Coder v1 Plan

## Task
Implement the UI module for PLang Runtime2: Liquid template rendering via Fluid library.

## Files Created
- `PLang/Runtime2/modules/ui/render.cs` — render action handler
- `PLang/Runtime2/modules/ui/providers/ITemplateProvider.cs` — provider interface
- `PLang/Runtime2/modules/ui/providers/FluidProvider.cs` — default Liquid provider

## Files Modified
- `PLang/Runtime2/Engine/Providers/this.cs` — register ITemplateProvider + type mapping
- `PLang/PLang.csproj` — add Fluid.Core 2.31.0
- `PLang.Tests/Runtime2/Modules/ui/RenderTests.cs` — implement 29 C# tests
- `Tests/Runtime2/Ui/` — implement 5 PLang test goals + fixture files

## Key Decisions
- `IsFile` property (bool?) for explicit file/inline control
- `RegisterExpressionTag` for callGoal (supports both quoted and unquoted goal names)
- `PlangFileProvider` bridges IPLangFileSystem to Microsoft.Extensions.FileProviders.IFileProvider
- `PathData` reused for path resolution
- Fluid 2.31.0 required (2.11.1 has .NET 10 compatibility bug with include tag parsing)
- HTML encoding on by default via HtmlEncoder.Default
- ResolveType mapping: "template" (not "ui")
