# Plan: v5 — Handlers pass `this` to Path (OBP rule 2)

## Changes

### 1. Path.cs — Accept action records instead of decomposed params
- `Copy(Path, bool, bool)` → `Copy(actions.file.Copy action)`
- `Move(Path, bool)` → `Move(actions.file.Move action)`
- `Delete(bool, bool)` → `Delete(actions.file.Delete action)`
- `List(string, bool)` → `List(actions.file.List action)`
- `Save(object)` → `Save(actions.file.Save action)`
- `AsFile()` → renamed to `Exists()`
- `Read()` — unchanged

### 2. Handlers — all pass `this`
- save.cs: `Path.Save(this)`
- delete.cs: `Path.Delete(this)`
- copy.cs: `Source.Copy(this)`
- move.cs: `Source.Move(this)`
- list.cs: `Path.List(this)`
- exists.cs: `Path.Exists()`

### 3. PathTests.cs — Create action records for behavior tests
Tests that call Copy/Move/Delete/List/Save now construct action records with init properties. Rename AsFile → Exists test.

### 4. FileHandlerTests.cs — No changes needed
Handler Run() signatures unchanged. Internal delegation is transparent.

## Verification
1. `dotnet build PLang/PLang.csproj` — 0 errors
2. `dotnet build PLang.Tests/PLang.Tests.csproj` — 0 errors
3. `dotnet run --project PLang.Tests` — all tests pass
