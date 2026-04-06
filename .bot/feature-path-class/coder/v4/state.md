# State — v4

## Status: Complete

## What was done
- Path.cs: stores Engine.@this instead of IPLangFileSystem. Constructor takes engine. Save() drops engine param. Delete() absorbs ignoreIfNotFound. Added AsFile().
- save.cs: `Path.Save(Value)` — one-liner
- delete.cs: `Path.Delete(Recursive, IgnoreIfNotFound)` — one-liner
- exists.cs: `Path.AsFile()` — one-liner
- PathTests.cs: Added _engine field, all `new PLangPath(_, _fs)` → `new PLangPath(_, _engine)`, all System.IO → _fs (except constructor/Dispose), Save tests no longer create separate engines, added Delete_IgnoreIfNotFound and AsFile tests
- FileHandlerTests.cs: All System.IO → _fs, MakePath/MakeAbsPath use _engine

## Build & test results
- PLang.csproj: 0 errors
- PLang.Tests.csproj: 0 errors
- Tests: 1221/1221 passing (was 1219, +2 new tests)

## Files modified
- `PLang/App/Engine/Memory/Path.cs`
- `PLang/App/actions/file/save.cs`
- `PLang/App/actions/file/delete.cs`
- `PLang/App/actions/file/exists.cs`
- `PLang.Tests/App/Modules/Path/PathTests.cs`
- `PLang.Tests/App/Modules/file/FileHandlerTests.cs`

## Blockers: None
