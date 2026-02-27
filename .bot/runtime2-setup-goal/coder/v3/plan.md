# v3 Plan — Share SettingsData across all actors

## Problem
SettingsData registered only on System actor's MemoryStack. PLang code runs on User context. `%Settings.ApiKey%` silently returns null.

## Design decision (from Ingi)
Settings are runtime-wide (e.g., API keys). All actors should get the same SettingsData object — same reference, same backing DataSource.

## Changes

### 1. Engine owns the single SettingsData instance
- Add `internal SettingsData SettingsVariable { get; }` to Engine
- Create in constructor: `SettingsVariable = new SettingsData(this);`

### 2. Actor always registers the shared instance
- Remove System-only `if` block in Actor constructor
- Always: `Context.MemoryStack.Put(engine.SettingsVariable);`

### 3. Fix tests to use User context
- Change all `_engine.System.Context.MemoryStack` references to `_engine.Context.MemoryStack` (User's stack)
- Add test: all actors share the same SettingsData object (reference equality)

## Files
- `PLang/Runtime2/Engine/this.cs` — add SettingsVariable
- `PLang/Runtime2/Engine/Context/Actor.cs` — remove System-only check
- `PLang.Tests/Runtime2/Modules/settings/SettingsDataTests.cs` — fix all tests + add shared object test
