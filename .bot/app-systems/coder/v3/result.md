# coder v3 — stage 3 result: one set of types

Branch `app-systems`. The plan's stage 3 row and the rulings (Ingi, relayed by plang-40) are in
`.bot/app-systems/architect/plan.md`.

## Commits
| commit | what |
|---|---|
| 4bfd09c61 | one set of types: each type owns its name, aliases, C# class and facts; `IMatch<TSelf>`; `Add` is the one way in |
| 35e553e62 | `ClrType` is the item class; types are born through the list |
| d7c34770d | kinds join the store and own their type (number, hash); aliases; `kind.list(context)` |
| bfb8b1d8e | choice's sets and path's schemes are kinds |
| a56192aca | a type's `kind` is never null (lowercase, the empty kind) |
| f88e1f8d3 | the name door is the one door; `Clr(name)` moves to the tests |

## What changed
- **One set.**
  - The registry's seven stores (name↔class maps, runtime map, clr owners, the catalog, the full-type cache, the CLR-name set) are one list of types. A copy-on-write array is walked by every lookup.
  - A type is born from its class (`new type(name, clr, types)`) with its `Alias` (a static on the class), its owned C# shapes (`OwnedClrTypes`), and the facts its class declares.
  - `IMatch<TSelf>` arrives: `type.Match(key)` answers for the type's name or an alias.
- **`Add` is the one way in**, for a class or an assembly with its renderers, and it answers errors as results:
  - sealed names (refused even for a non-item class that claims one);
  - reserved property names;
  - one name, one class;
  - renderer coverage.

  The static `Loader` is gone; `code.load` calls `app.Type.Add(assembly, context)`.
- **Spellings.**
  - Real aliases now sit on their types: text←string, bool←boolean, dict←dictionary/map, list←array, binary←bytes.
  - Precisions are number's kinds (`integer` answers the int kind).
  - csv/txt/xml/yaml/yml are text's kinds, not names.
  - Nullable spellings are dropped.
  - The `text/md` and `list<path>` splits and the generic `List<T>` door are gone. The formal reader parses its own `: list<text>` syntax and asks by identity.
- **`ClrType` is the item class.** Every item's own type passes `typeof(@this)`. The identity door no longer stamps a C# mate, and the type statics are gone. A typed null keeps its declared type. A `Data` born with a classless declared type births it through the list with its context. `Create`/`Empty`/`Read`/`Creatable` no longer ask the registry.
- **Kinds.**
  - `number.kind` derives `type.kind`; hash's algorithms are kind classes. Each kind class declares its type (`Owner`), and a classless kind is the readers'/formats' as before.
  - Closed sets are kinds of choice; path schemes are kinds of path (`Kind.Add(kind)`, `Kind.Add(assembly)`).
  - `kind.list(context)` answers a type's kinds as full types.
  - `type.kind` is never null: the empty kind (`kind.empty`) knows its type and is never written.
  - The kind store stays at `app.type.Kind` until stage 4 (plang-40's decision).
- **Gone:** `Kinds`, `Richness`, `primitive/this.cs`, `choice/list/this.cs`, `Loader.cs`, `ResolveType`, `Register`/`RegisterRuntime`, `KnownTypes`, `Get(string)`, and `Clr(name)` (now a test extension).

## Verification
- **Six C# suites** after each commit: no new failures. Final counts: Modules 38, Types 19, Wire 18, Data 44, Generator 18, Runtime 24 (baseline: 38 / 23 / 18 / 45 / 18 / 24). The only transient red was the known load flake `Run_ParallelExecution_RespectsSemaphoreLimit`.
- **Prompt twins:** `PickListTests` (stages 1 and 2, and prompt C) byte-equal after every commit.
- **`plang --test`** after a clean rebuild: 7 pass / 0 fail / 317 stale, the same as the baseline.
- **Tests:** ~90 test files were updated to the new model by a helper agent, and I reviewed the results. Tests that only pinned removed behaviour were deleted and listed in the agent reports: int/long/nullable names, generic `Clr`, the `a/b/c` split, reference-identity caching, and the DLL overriding "int".
- **New tests:** `KindListTests`.

## Notes for the next stages
- Prebuilt fixture DLLs (`Shared/Fixtures/dlls`: TypeProvider, SignatureRendererShadow) were built against a namespace that no longer exists. Their tests were already red in the baseline; they need rebuilding from `TestFixtures/`.
- `set.@this` has two private static helpers (`Closed`, `Choices`), needed because the base constructor takes the name before the instance exists.
- The identity door builds a fresh full type per ask (no cache, per the plan); measure if it shows up.
