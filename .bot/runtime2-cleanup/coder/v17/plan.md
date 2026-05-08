# Stage 17 — coder plan (`builder-tester-rename`)

Rule D — gerund-named app-graph properties become nouns.

| | Today | After |
|--|--|--|
| Folder | `App/Build/` | `App/Builder/` |
| Folder | `App/Test/` | `App/Tester/` |
| Namespace | `App.Build` | `App.Builder` |
| Namespace | `App.Test` | `App.Tester` |
| Property | `app.Build` | `app.Builder` |
| Property | `app.Testing` | `app.Tester` |
| CLI | `plang --build` / `plang build` | `plang --builder` (legacy preserved) |
| CLI | `plang --test` | `plang --tester` (legacy preserved) |
| Type | `TestFile` | `App.Tester.File` |
| Type | `TestRun` | `App.Tester.Run` |
| Type | `TestStatus` | `App.Tester.Status` |
| Global alias | `Testing = App.Test.@this` | `Tester = App.Tester.@this` |

## Files moved

- `App/Build/` → `App/Builder/` (2 files).
- `App/Test/` → `App/Tester/` (7 files; 3 also dropping the `Test` prefix: `TestFile.cs` → `File.cs`, `TestRun.cs` → `Run.cs`, `TestStatus.cs` → `Status.cs`).

## Production sweep

- 10 production files updated (broad `.Testing\b` / `.Build\b` regex sweep with caveats).
- App.this.cs: `public Tester Tester { get; }` → `public global::App.Tester.@this Tester { get; }` (alias-vs-namespace ambiguity resolved by full qualification, mirroring the existing Builder pattern).
- App.this.Snapshot.cs: `Build.Capture(...)` → `Builder.Capture(...)`, `Testing.Capture(...)` → `Tester.Capture(...)`.
- Builder/this.Snapshot.cs and Tester/this.Snapshot.cs: `ctx.App.Build` → `ctx.App.Builder`, `ctx.App.Testing` → `ctx.App.Tester`.
- modules/test/* — `App.Test.X` references swept; `Test.@this` and `Test.Coverage` qualified to `App.Tester.@this` / `App.Tester.Coverage`.
- Section keys in Snapshot writes/reads kept as `"Build"` and `"Testing"` strings (snapshot wire-format compatibility — values stored on disk; renaming would break round-trip).

## CLI

- `Executor.cs:34` — `plang build` legacy → `--builder`.
- `Executor.cs:Configure` — `!test`/`!tester` and `!build`/`!builder` parameter keys both honored (canonical is the `--er` form; legacy preserved).
- `RegisterStartupParameters.cs:47` — `--builder` accepted alongside `--build`.
- Both `plang --test` and `plang --tester` produce identical results (verified).

## Test sweep (36 files)

`PLang.Tests/` — broad regex pattern across `\.Testing\b` / `\.Build\b` plus type-name renames.

Specific patches needed for:
- `JsonStreamSerializerTests.cs` — local `private enum TestStatus` got mis-renamed to a qualified declaration; renamed to `LocalStatus` and updated its single usage.
- `OperatorTests.cs` — three sites of double-prefixed `App.Tester.global::App.Tester.Status` collapsed to `App.Tester.Status`.
- `Schema/SchemaTests.cs` — false-positive `Schema.Build()` → `Schema.Builder()` reverted (Build is a method, not a property).
- `Modules/this.cs` and `Schema/this.cs` doc comments — same false-positive reverted.
- `ExecutorTests.cs`, `ActorSettingsStoreTests.cs` — `engine!.Testing` / `engine!.Build` patterns swept.

Generator test fixtures contain sample source strings using `namespace App.Test` — these are sandbox identifiers in the fixture text, not real callers; left in place.

## Verification

- `find PLang/App/Build` and `find PLang/App/Test` — empty.
- `grep -rn "\bApp\.Build\b" PLang/ PLang.Tests/ --include='*.cs'` (excluding `App.Builder`) → 0.
- `grep -rn "\bApp\.Test\b" PLang/ PLang.Tests/` (excluding generator fixtures and `App.Tester`) → 0.
- `grep -rn "TestFile\b\|TestRun\b\|TestStatus\b" PLang/ PLang.Tests/` → 0.
- C# 2752/2752; PLang 199/199.
- Both `plang --test` and `plang --tester` produce 199/199.
