# v2 Plan: Fix namespace string literals for app-lowercase rename

## Task
Fix all `"App."` string literals in test files and production code after the `App` → `app` namespace rename.

## Scope
- Test files: update `"App.X"` string literals in reflection calls and assertions
- Generator test stubs: update inline Roslyn compilation source from `App.modules` → `app.modules`
- Fixture DLLs: rebuild TestProvider and NoCtorProvider with lowercase namespaces
- Production code: fix `StoreOnlyModifier` in `app/Builder/this.cs` that was checking `"App.Goals"` (old namespace)

## Files to modify
1. `PLang.Tests/App/Modules/signing/SignatureRenameTests.cs` — reflection GetType() calls
2. `PLang.Tests/App/Utility/TypeMismatchExample.cs` — Contains() assertion
3. `PLang.Tests/App/Utility/TypeMismatchMessageTests.cs` — Contains() assertion  
4. `PLang.Tests/App/Fixtures/MatrixRunner.cs` — namespace prefix strings
5. `PLang.Tests/Generator/IncrementalCacheTests.cs` — ActionClassInfo stubs + MinimalSource
6. `PLang.Tests/Generator/GeneratorValidationTests.cs` — file names + inline source stubs
7. `PLang.Tests/Generator/SnapshotParamsTests.cs` — generated file names
8. `PLang.Tests/Generator/Diagnostics/Plng001PostMigrationTests.cs` — Stubs + inline sources
9. `TestFixtures/TestProvider/TestSigningProvider.cs` — using directives
10. `TestFixtures/NoCtorProvider/NoCtorProvider.cs` — using directives
11. `PLang/app/Builder/this.cs` — StoreOnlyModifier namespace check

## Verification
Target: 2752/2752 passing
