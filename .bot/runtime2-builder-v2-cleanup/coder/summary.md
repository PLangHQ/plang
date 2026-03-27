# Coder Summary — runtime2-builder-v2-cleanup

## v1: Engine Folder Cleanup — Errors, Types, PrParser
Renamed `DataSourceError` → `SettingsError` (consistent with DataSource→Settings rename). Consolidated TypeMapping/Types duplication — Types/@this now delegates to TypeMapping for name↔type, keeps unique extension/kind/mime logic. Deleted unused Runtime2 PrParser (zero refs, used System.IO). See [v1/summary.md](v1/summary.md).

## v2: Module Cleanup & Data Pattern Consistency
Comprehensive pass over all modules enforcing consistent patterns: GoalCall.Parameters → List<Data>, [Provider] attribute for all handler DI, IdentityVariable → IdentityData : Data, signing pipeline moved to provider, library renamed to module, variable module simplified. Added DataList<T>, Data.FromError<T>(), Data.ToError<T>(). 14 commits, net reduction ~200 lines. One known test issue (step runner condition check). See [v2/summary.md](v2/summary.md).
