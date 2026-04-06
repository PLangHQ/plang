# Output Summary

## v13 — Remove Core/ folder, promote to App/
Eliminated Core/ folder. Moved 26 files to App/ subfolders (Goals/, Steps/, Actions/, Events/, Cache/, Execution/). Changed namespace to App. Updated ~80 files. Zero build errors.
See [v13/summary.md](./v13/summary.md)

## v14 — Law of Names + modules→actions + Serializers→Channels (ALL COMPLETE)
Full "Law of Names" restructuring in four phases + follow-up refactorings. Phase 1: namespace migration + folder restructure (212 files). Phase 2: file organization, dot-naming, Events→EngineEvents. Phase 3: class renames to {Owner}{Capability}. Phase 4: EngineDebug/EngineTesting static→instance. modules→actions rename, Library/EngineLibraries move to Engine/, Attributes merge. Serializers→Channels: moved serializer files under Channels subsystem, rewired ownership so `engine.Channels.Serializers` is the access path. All 1167 tests passing across all changes.
See [v14/summary.md](./v14/summary.md)
