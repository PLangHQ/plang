# v29 plan — `[Provider]` → `[Code]` rename

## Why

The runtime escape-hatch was renamed `App.Providers` → `App.Code` in an earlier
sweep — call sites already use `app.Code.Get<T>()`. The attribute name lagged,
leaving a contradictory shape: `[Provider] IFoo Foo { get; init; }` resolves
through `app.Code`. v29 closes the gap.

## Scope

- **Attribute** (`PLang/App/modules/Attributes.cs:48`): `ProviderAttribute` →
  `CodeAttribute`. Update XML doc — drop "provider" wording, name the resolution
  path explicitly (`app.Code.Get<T>()`).
- **All `[Provider]` usages** (50 files in `PLang/App/modules/**`) →  `[Code]`.
- **Source generator** (`PLang.Generators/`):
  - `Discovery/this.cs:127, 149`: short-name match `"ProviderAttribute"` →
    `"CodeAttribute"`.
  - `Discovery/this.cs:45-46`: PLNG001 title + messageFormat: `[Provider]` →
    `[Code]`.
  - `Discovery/this.cs:40, 89, 122, 147`: doc/comment text.
  - `Emission/Property/Provider/` folder → `Emission/Property/Code/`.
    Namespace `PLang.Generators.Emission.Property.Provider` →
    `PLang.Generators.Emission.Property.Code`.
  - Aliases `using ProviderProperty = ...` → `using CodeProperty = ...` and
    rename the local identifiers (`isProvider`, `ProviderProperty` type usage).
  - `Emission/Property/this.cs:7` doc.
  - `Emission/Action/this.cs:143, 184`: `ProviderProperty` references → `CodeProperty`.
- **Docs** (`Documentation/v0.2/`):
  - `architecture.md:236, 250, 263, 265` — `[Provider]` and folder reference.
  - `good_to_know.md:611, 637, 639, 725` — same.
  - `action-catalog.md:66`.
  - `builder-self-rebuild-plan.md:39`.
- **CLAUDE.md proposal** (NOT direct edit): append to
  `.bot/runtime2-cleanup/claude-md-proposals.md` for `/CLAUDE.md:39` change
  (`[Provider] T` → `[Code] T`). Docs picks it up at merge.

## Out of scope

- `App.Providers.Get<T>()` runtime call sites — already converted.
- `FileProvider` / `IFileProvider` (Microsoft.Extensions.FileProviders) and
  `DefaultFileProvider` — distinct concept, leave alone.
- Comments in `Errors/Error.cs:23` ("Providers attach things") — unrelated
  generic English usage. Leave.

## Strategy

1. Rename attribute class (single line).
2. Sweep all `[Provider]` → `[Code]` usages with `sed` across `PLang/App/modules/`.
3. Generator: rename folder via `git mv`, update namespace + aliases + match
   strings + diagnostic message + comments.
4. Docs swept the same way.
5. Build clean → both test suites green.
6. CLAUDE.md proposal entry.

## Verify

- `rm -rf */bin */obj && dotnet build PlangConsole` → 0 errors.
  *(Critical — generator string-match changes are runtime, not type-checked.)*
- `dotnet run --project PLang.Tests` → 2752/2752.
- `cd Tests && ../PlangConsole/bin/Debug/net10.0/plang --test` → 199/199.

## Commit

`coder v29: rename [Provider] → [Code], align attribute with app.Code runtime`
