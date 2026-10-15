# Stage 1 — a type is named by its path from a root (decisions 752, 761, 762, 763, 768, 769)

Trace: `../stage1-type-names-trace.md`. Rulings relayed by the architect, 2026-10-14/15.

## Commit A — `error` is its folder's `@this` (decision 761/OPEN 6)
- `PLang/app/error/Error.cs` → `PLang/app/error/this.cs`, class `@this`; a global alias `Error` for C# sites.
- The 299 fully qualified `app.error.Error` uses in 137 production files: Edit, one file at a time. 44 test files batched.
- Behaviour-neutral: the plang name stays `error`.

## Commit B — the stage
1. One rule in `item.IdentityOf`: `app.type.item` → `item`; `app.type.item.X` → `X`; `app.module.M[.type].X` → `M.X`;
   `app.X` → `X`, unless that equals one of plang's own names (then it keeps `app.`: `app.setting`); other assemblies
   as is; a nested class reads through its declaring class. `NameOf` answers it; `[PlangType]` goes from item types
   (closed sets keep it until stage 2).
2. Module main types move to their module folder (761): screen, window, browser, archive, identity; their kinds and
   code follow (`screen/window`, `screen/display`, `screen/view`, `screen/code`).
3. Settings classes are types of their own; `Path` is the type name (763); `setting/kind/` and `Registry.cs:330–334`
   go; `Class(path)` is `type.list[path]`; `Own` and making the instance move onto the settings type.
4. Registry: a type is an `@this` item, or a named settings class; claims its name and aliases only (no namespace,
   769c); the guard refuses a clash naming both.
5. The type's Out face writes `name` only (769c); `type.Namespace` goes.
6. Readers in a serializer folder register under the rule name of the one item type the folder reads (b).
7. The ~18 C# string sites take the new names.
8. Surface golden re-pinned naming 752, 761, 769.

## Checks
Stage 1 tests (SettingClassIsAType, ModuleTypeIsNamedByItsModule, ShortNameFindsTheType) green; the C# sweep by name
(only the 6 carried + the test-designer's stage-3 reds); plang suite against 249/29/12/3, only the two stage 1 tests
moving; the list of `.pr` files to rebuild sent to the architect.
