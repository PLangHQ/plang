# Stage 19 — coder plan (`provider-to-code-rename`)

End-to-end Provider → Code rename. Final stage of the cleanup branch.

## Folder relocations (13)

```
App/Providers/                       → App/Code/
App/Data/Providers/                  → App/Data/Code/
App/modules/provider/                → App/modules/code/
App/modules/{assert,builder,condition,crypto,file,http,identity,llm,signing,ui}/providers/
                                     → App/modules/X/code/
```

## Type renames

**Marker interface:**
- `IProvider` → `ICode` (in `App/Code/ICode.cs`).

**Per-module interfaces (drop `Provider` suffix):**
- `IAssertProvider` → `IAssert`, `IBuilderProvider` → `IBuilder`,
  `ICryptoProvider` → `ICrypto`, `IFileProvider` → `IFile`,
  `IHttpProvider` → `IHttp`, `IIdentityProvider` → `IIdentity`,
  `ILlmProvider` → `ILlm`, `ISigningProvider` → `ISigning`,
  `IKeyProvider` → `IKey`, `ITemplateProvider` → `ITemplate`,
  `IGrepProvider` → `IGrep`. `IEvaluator` unchanged (already correct).

**Implementations (drop both `Default` and `Provider` suffixes):**
- `OpenAiProvider` → `OpenAi`, `FluidProvider` → `Fluid`, `Ed25519Provider` → `Ed25519`.
- `DefaultGrepProvider` (in Data/Code/) → `Default` (named `Grep` in the brief but that collides with the interface's `Grep(...)` method — picked `Default` instead).
- `DefaultBuilderProvider`, `DefaultHttpProvider`, `DefaultIdentityProvider`,
  `DefaultAssertProvider`, `DefaultFileProvider`, `DefaultEvaluator`,
  `DefaultProvider` (crypto) → all become `Default.cs` in their `code/` folder.

**App property:** `app.Providers` → `app.Code`. Global alias `AppProviders = App.Providers.@this` → `AppCode = App.Code.@this`.

## Source generator

`PLang.Generators/Emission/Property/Provider/this.cs` and
`Emission/Action/this.cs` had hardcoded `app.Providers.Get<T>()` strings
in their emit templates — updated to `app.Code.Get<T>()`. Without this,
every generated handler with a `[Provider]` property would break.

## Subtle shadowing fixes

- `App.@this`: the `Code` property shadows the `App.Code` namespace within
  App's own files. Internal refs to types in `App.Code` namespace use
  `global::App.Code` qualification.
- `App/Code/this.cs:RegisterDefaults` had `new Default()` for both crypto
  and identity (ambiguous since both module folders have `Default.cs`).
  Fully-qualified each: `new modules.identity.code.Default()`,
  `new modules.crypto.code.Default()`, etc.
- `App/Code/this.cs:RegisterDefaults` `new Fluid()` — `Fluid` here is the
  NuGet namespace; class is `App.modules.ui.code.Fluid`. Qualified as
  `new modules.ui.code.Fluid()`.
- `App/modules/ui/code/Fluid.cs`:
  - The class `Fluid` shadows the `Fluid` package namespace internally.
    Qualified `Fluid.Ast.Expression` → `global::Fluid.Ast.Expression`.
  - Also `private sealed class PlangFileProvider : IFileProvider` (the
    Microsoft.Extensions.FileProviders interface) — sed initially renamed
    `IFileProvider` → `IFile` blindly; reverted manually.
- `App/Data/Code/Default.cs` (was `DefaultGrepProvider.cs`) — kept name as
  `Default`, not `Grep`, because `IGrep` interface has a `Grep(...)` method
  and a class can't have a member named after itself.
- `App/Data/this.Navigation.cs:179` `new Providers.Grep()` reference fixed
  to `new App.Data.Code.Default()`.
- `App/Code/this.cs:254` had a double-prefix `Data.App.Data.Code.IGrep` from
  sed; manually fixed to `global::App.Data.Code.IGrep`.

## Test fixture DLLs rebuilt

`TestFixtures/{TestProvider,EmptyProvider,NoCtorProvider}` projects had
hardcoded `App.modules.signing.providers.ISigningProvider` references.
After namespace updates and rebuild + redeploy to
`PLang.Tests/App/Fixtures/dlls/`, the load tests pass.

## Caller sweep stats

- 120+ files swept across PLang/ and PLang.Tests/.
- Mass sed pass with fully-qualified `global::App.X.Y` form for the bulk.
- Manual fix-ups for the shadowing cases above.

## Verification

- `find PLang/App -type d -name "providers" -o -name "Providers"` → empty.
- `grep -rn "\bIProvider\b" PLang/ PLang.Tests/ --include='*.cs'` → 0 (only `ICode`).
- `grep -rn "\bApp\.Providers\b\|\bapp\.Providers\b" PLang/ PLang.Tests/` → 0.
- `grep -rn "\bDefaultBuilderProvider\|\bDefaultHttpProvider\|\bOpenAiProvider\|\bFluidProvider\|\bEd25519Provider" PLang/ PLang.Tests/ --include='*.cs'` → 0.
- C# 2752/2752; PLang 199/199; build clean.

## Brief deviations

- `DefaultGrepProvider.cs` — brief said `Grep.cs` (variant name); used `Default.cs` instead because the class can't have a method matching its own name (`IGrep.Grep(...)` interface method).
- Filters/this.cs collection in stage 15 — skipped (static utilities don't need a registry); same applies here for any "should we add a `Code` aggregate?" — `app.Code` is already the registry.

## End of branch

This is stage 19 — the last carved stage. After this commit lands, all
22 stages are done.
