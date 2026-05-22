# codeanalyzer v1 — path-polymorphism review plan

## Scope
Review the path-polymorphism branch (8 stages, ~6k lines changed). New
abstract `path` type with `FilePath`/`HttpPath` subclasses, per-App scheme
registry, file action handlers collapsed, `System.IO.Abstractions` wrapper
layer deleted.

## Files in primary scope
- `PLang/app/types/path/this.cs` — abstract path base
- `PLang/app/types/path/this.Operations.cs` — abstract verb surface + cross-scheme defaults
- `PLang/app/types/path/this.Authorize.cs` — Permission gate
- `PLang/app/types/path/scheme/this.cs` — scheme registry
- `PLang/app/types/path/PathSchemeAttribute.cs`
- `PLang/app/types/path/file/this.cs`, `this.Operations.cs`, `this.Validate.cs`
- `PLang/app/types/path/http/this.cs`
- `PLang/app/types/path/permission/this.cs` (GlobMatches rewrite)
- `PLang/app/modules/file/{read,save,copy,move,delete,exists,list}.cs`
- `PLang/app/types/Conversion.cs` (path dispatch block)
- `PLang/app/this.cs` (scheme registration)

## Passes
1. OBP compliance (1a rules + 1b shape smells)
2. Simplification
3. Readability
4. Behavioral reasoning — cross-scheme correctness of the new polymorphism
5. Deletion test

## Verification
- Clean rebuild + `dotnet build PlangConsole`
- `plang --test` from `Tests/`

## Status
Build green. plang --test 202 pass / 0 fail / 1 stale (documented pre-existing).
Findings below — see report.md.
