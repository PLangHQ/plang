# v3 Plan — Phase 4 namespace rename: fix type-position references

## Task
Fix ~80 source files where type-position bare references use old PascalCase names
after top-level `app/X/` folders renamed from PascalCase to lowercase.

## Key rules
1. Type-position references (class inheritance, parameter types, return types, local vars) — fix to lowercase
2. Expression-position property accesses (app.Goals.X, app.FileSystem.Y, app.Events.Z) — keep PascalCase
3. Global aliases (Snapshot, Variables, Step, Channel, Serializers, GoalCall, etc.) — use alias name alone, NOT `Alias.@this`
4. `app.callstack.call.Errors` namespace — stays PascalCase (exception — file declares it that way)
5. When relative resolution fails — use `global::app.x.y.z.@this`

## Approach
- Read each file, identify type-position uses of old names, fix precisely
- Verify with `dotnet build PLang/PLang.csproj` after each batch
- Final check: full solution build

## Result
0 errors, 46 warnings. PLang.csproj and PLangConsole build clean.
