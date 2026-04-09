# Coder Summary — runtime2-builder-v2-cleanup

## v1: Engine Folder Cleanup — Errors, Types, PrParser
Renamed `DataSourceError` → `SettingsError` (consistent with DataSource→Settings rename). Consolidated TypeMapping/Types duplication — Types/@this now delegates to TypeMapping for name↔type, keeps unique extension/kind/mime logic. Deleted unused App PrParser (zero refs, used System.IO). See [v1/summary.md](v1/summary.md).

## v2: Module Cleanup & Data Pattern Consistency
Comprehensive pass over all modules enforcing consistent patterns: GoalCall.Parameters → List<Data>, [Provider] attribute for all handler DI, IdentityVariable → IdentityData : Data, signing pipeline moved to provider, library renamed to module, variable module simplified. Added DataList<T>, Data.FromError<T>(), Data.ToError<T>(). Fixed step runner child-skipping to only apply to condition module steps. 15 commits, all 1839 tests pass. See [v2/summary.md](v2/summary.md).
