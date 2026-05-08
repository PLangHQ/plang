# Stage 21 — coder plan (`navigators-to-variables`)

Move `App/Data/Navigators/` → `App/Variables/Navigators/`. Pure
structural relocation; instance stays App-level (shared); access path
gains the canonical `app.Variables.Navigators` form alongside the
existing `app.Navigators`.

## Files

- 7 files relocated (`git mv App/Data/Navigators App/Variables/Navigators`); namespace `App.Data.Navigators` → `App.Variables.Navigators` on each.
- `App/this.cs:126` — property type `Data.Navigators.@this` → `Variables.Navigators.@this`.
- `App/Variables/this.cs` — new delegating property `public Navigators.@this Navigators => _context!.App.Navigators;`. Same instance; canonical access path.
- `App/Data/this.Navigation.cs:281` — unqualified `Navigators.ValueNavigators` → `Variables.Navigators.ValueNavigators` (inside `App.Data` namespace, the unqualified `Navigators` previously resolved to `App.Data.Navigators`).
- `PLang.Tests/App/DataTests/Navigators/JsonObjectNavigationTests.cs:1` — `using global::App.Data.Navigators;` → `using global::App.Variables.Navigators;`.

## Verification

- `find PLang/App/Data/Navigators -type f` → empty.
- `find PLang/App/Variables/Navigators -type f` → 7 files.
- `grep -rn "App\.Data\.Navigators" PLang/ PLang.Tests/ --include='*.cs'` → 0.
- C# 2752/2752; PLang 199/199; build clean.
