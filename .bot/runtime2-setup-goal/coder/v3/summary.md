# v3 Summary — Share SettingsData across all actors

## What this is
Fixes a bug where `%Settings.ApiKey%` silently returned null in PLang code. SettingsData was only registered on the System actor's MemoryStack, but PLang code executes on the User actor's context.

## What was done
- **Engine owns single SettingsData instance** — Added `internal SettingsData SettingsVariable` to Engine, created in constructor. One object shared by all actors.
- **Actor always registers it** — Removed System-only `if` block in Actor constructor. Every actor's MemoryStack gets the same SettingsData reference.
- **Fixed all tests** — Changed from `_engine.System.Context.MemoryStack` to `_engine.Context.MemoryStack` (User's stack, which is what PLang code actually uses).
- **Added 2 new tests** — `SettingsData_SameObjectAcrossAllActors` (reference equality) and `SettingsData_SetViaSystem_ReadableFromUserContext`.

## Files modified
- `PLang/Runtime2/Engine/this.cs` — added `SettingsVariable` property and constructor init
- `PLang/Runtime2/Engine/Context/Actor.cs` — removed System-only check, always registers shared SettingsData
- `PLang.Tests/Runtime2/Modules/settings/SettingsDataTests.cs` — all tests use User context, 2 new tests

## Code example

```csharp
// Engine owns the single instance
internal SettingsData SettingsVariable { get; }
// In constructor:
SettingsVariable = new SettingsData(this);

// Actor registers it (all actors, same object)
Context.MemoryStack.Put(engine.SettingsVariable);
```

## Test results
- C# tests: 1478/1478 pass
