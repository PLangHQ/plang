# Coder Summary — runtime2-builder-v2-cleanup

## v1: Engine Folder Cleanup — Errors, Types, PrParser
Renamed `DataSourceError` → `SettingsError` (consistent with DataSource→Settings rename). Consolidated TypeMapping/Types duplication — Types/@this now delegates to TypeMapping for name↔type, keeps unique extension/kind/mime logic. Deleted unused Runtime2 PrParser (zero refs, used System.IO). See [v1/summary.md](v1/summary.md).
