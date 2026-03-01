# Code Analyzer v4 — runtime2-setup-goal

## Overall Verdict: CLEAN

v3 finding addressed correctly. No new issues.

---

## v3 Finding 1 (High): SettingsData bridge unreachable from PLang code
**Status: FIXED**

Two-part fix:

**Engine owns the single instance** (Engine/this.cs:121, 206):
```csharp
internal SettingsData SettingsVariable { get; }
// In constructor:
SettingsVariable = new SettingsData(this);
```

**Actor always registers it** (Actor.cs:64-66):
```csharp
// Register shared SettingsData — same object for all actors.
// %Settings.ApiKey% resolves identically in User, Service, and System contexts.
Context.MemoryStack.Put(engine.SettingsVariable);
```

The System-only `if` block is removed. All actors get the same SettingsData reference.

### Verification

- `SettingsVariable` is `internal` — correct scope, only Actor needs it
- Created in Engine constructor before actors exist (actors are lazy: `_system ??= new Actor(...)`)
- SettingsData constructor only stores the engine reference; `GetChild` accesses `_engine.System.DataSource` lazily — no circular initialization
- All 16 test methods now use `_engine.Context.MemoryStack` (User's stack), matching real PLang execution
- Two new tests prove the fix:
  - `SameObjectAcrossAllActors` — reference equality across User, System, Service
  - `SetViaSystem_ReadableFromUserContext` — write via System DataSource, read from User and Service MemoryStack

### Constructor ordering check

Engine constructor line 206 creates `SettingsVariable = new SettingsData(this)` before `FileSystem` is set (line 209). Safe because SettingsData only stores the reference — `GetChild` accesses `_engine.System.DataSource` lazily, and `DataSource` uses `Engine.FileSystem` lazily via `Lazy<IDataSource>`.

---

## v3 Observation (Low): MemoryStack.Clone shared reference mutation

Still present but now less concerning — since all actors share the same SettingsData instance, the Context stamping in `Clone()` doesn't cross actor boundaries in a way that matters. The shared object's `Context` gets updated to whoever cloned last, but SettingsData.GetChild only uses `Context` for stamping child Data objects (line 66: `child.Context = Context`). Acceptable.
