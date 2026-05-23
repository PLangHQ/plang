# Coder v1 — path-polymorphism

Implement the architect's 7-stage plan from `.bot/path-polymorphism/architect/`.

## Stage-by-stage status

| # | Stage | Status |
|---|-------|--------|
| 1 | Namespace move `app.filesystem` → `app.types.path` + `@this` convention | **DONE** |
| 2 | `path` abstract + FilePath + Scheme registry | **DONE (structural + 21/31 tests green)** |
| 3 | Handler one-liners + delete IFile/DefaultFileProvider | **NOT STARTED** |
| 4 | `[PathScheme]` attribute (marker only) | **DONE** |
| 5 | HttpPath impl | **NOT STARTED** |
| 6 | Per-scheme `Absolute` canonical form | **NOT STARTED** |
| 7 | Contract tests + fixtures | **NOT STARTED** |

## Decisions

- **No global `Path` alias.** The architect plan calls for `global using Path = app.types.path.@this;` but it collides with `System.IO.Path` across dozens of test files. Reverted to per-file aliases where wanted; production code uses `global::app.types.path.@this` qualified or `FilePath` (file scheme) per-file alias. Documented in `PLang/app/GlobalUsings.cs`.
- **Abstract types with `[PlangType]` are now indexed by `app.types.Registry`.** Previously `IsAbstract && !IsSealed` was a hard skip; that broke PLang-name lookup for `path.@this` after Stage 2. Skip-rule narrowed to "abstract AND no [PlangType] declared".
- **Scheme registry factory signature is `Func<string, Context, Path>`** — not the architect-proposed `Func<string, Path>`. Path construction needs Context (Goal directory resolution, App.FileSystem, etc.); rather than push that into IContext post-construction state, the factory takes it explicitly. `Scheme.From(raw, context)` mirrors the existing `path.Resolve(raw, context)` shape.

## Test status

- Baseline before stage 1: 2855 green, 94 red (test-designer's planned reds).
- After stage 1 (rename): 2855 green, 94 red.
- After stage 2 (structural + SchemeRegistryTests + PathAbstractTests bodies): 2865 green.
- After stage 4 (attribute + tests bodies): **2875 green, 74 red**.

The 74 remaining reds are TDD placeholders for stages 3, 5, 6, 7 (handler shape, HttpPath, canonical form, contract tests).

## Deferred work

Stages 3, 5, 6, 7 are not yet implemented. The cleanest landing point now keeps Stage 1's namespace rename + Stage 2's abstract+registry foundation + Stage 4's attribute. A follow-up session picks up Stage 3.
