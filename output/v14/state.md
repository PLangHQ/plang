# v14 State — Law of Names + modules→actions + Serializers→Channels

## Status: ALL PHASES COMPLETE

### Done
- **Phase 0**: Green baseline verified (restore, build, 1167 tests pass)
- **Phase 1**: Folder restructure + namespace migration (commit `e14c87a6`, 212 files)
- **Phase 3**: Convention renames to `{Owner}{Capability}` (commit `b9e1bdb5`, 44 files)
- **Phase 2**: File organization — dot-naming, splits, Events→EngineEvents (commit `b1eb5e1b`, 18 files)
- **Phase 4**: Static→instance for EngineDebug/EngineTesting (commit `fe820f99`, 5 files)
- **Documentation**: README.md fully rewritten (commit `97ae82d3`)
- **modules→actions**: Folder rename, Library/EngineLibraries move, Attributes merge (commit `37f67bb3`)
- **Serializers→Channels**: Move Serializers into Channels subsystem (uncommitted, pending commit)

### Serializers→Channels Changes
Files moved:
- `Engine/Serializers/View.cs` → `Engine/View.cs` (namespace: PLang.Runtime2.Engine)
- `Engine/Serializers/*.cs` (5 files) → `Engine/Channels/Serializers/*.cs` (namespace: PLang.Runtime2.Engine.Channels)

Files modified:
- `Engine/Engine.cs` — removed Serializers field/property/param
- `Engine/Channels/EngineChannels.cs` — added Serializers property, changed internal refs
- `Engine/Context/PLangContext.cs` — system variable path
- `Engine/Goal.cs`, `Step.cs`, `Action.cs`, `GoalCall.cs` — removed old using
- `Engine/Utility/TypeMapping.cs` — changed using
- `actions/file/save.cs` — changed access path + using
- 4 test files — changed using + assertions
- 3 doc files — updated object graph references

### Final Verification
- PLang.csproj: 0 errors
- PLang.Tests.csproj: 0 errors
- 1167/1167 C# tests passing
- 0 references to old namespace `PLang.Runtime2.Engine.Serializers` remain
