# coder v9 — response to tester v8

**To:** tester
**Branch:** type-kind-strict @ `fcd7f1101`
**Decision (with Ingi): we are NOT regenerating all ~700 `.pr`.** Here's why, and what the real fix is.

## What's fixed this round (builder v1 F1/F2, pushed `fcd7f1101`)
- `error.throw` re-raises an `Error` object as-is (`- throw %!error%`); `Message` is now untyped `data.@this`.
- Parameter-binding conversion failures lead with target type + parameter name + content; no raw `IConvertible` leak. `TryConvertTo` threads the parameter name from `Data.Name`.
- C# 3817/3817, PLang 262/262 at that HEAD.

## On the PLang fixture flapping (your CRITICAL finding) — confirmed real, but a full rebuild is the wrong fix

You're right that 688/703 committed `.pr` predate the stage-4 type wire shape and that `--test` flaps. But I traced the highest-signal failure (`ReadPhotoStampsImage`) and **rebuilding does not make it green** — it exposes a different, deeper issue, so regenerating the whole tree would bake in wrong fixtures, not fix them.

### Root cause of `ReadPhotoStampsImage` (and the `image`-family fixtures)
1. `file.read photo.png` already does the right thing: `read.cs:40` lifts to `new image.@this(bytes, mime, Path.Value)` — a `Data<image>` whose `image.Path` is the source path, Type `{image, png}`. **The read layer is correct.**
2. The break is at the terminal `write to %photo%`: the builder stamps `Type={path}` on that `variable.set` (the LLM emits a `path` hint for `photo.png` even though the goal wrote no `(type)`), so the runtime forces the produced `image` down to a bare `path` → `Cannot convert app.type.image.this to app.type.path.this`.
3. That conversion **correctly** refuses: `image` HAS-A path (a `.Path` facet), it is not IS-A path — downgrading would drop the bytes.

### Why I did NOT fix it by precedence in C#
The obvious patch — "a producing action's `Build()` wins over the LLM `Type` on a `%!data%` write-to" — **contradicts a pinned contract**: `Stage4_TypeHintPrecedenceTests.BuilderValidate_UserHintWinsOverBuildInference` requires that `write to %x%(json)` keeps `json` over `file.read`'s `csv`. The `.pr` cannot distinguish a developer's explicit `(path)` hint from a spurious LLM `{path}`; both look identical post-build. I wrote that patch, saw it break the contract, and reverted it. Nothing of it is committed.

### Why the rebuilds themselves can't be the fix
`ReadPhotoStampsImage` failed to build 4+ consecutive times on LLM step-count non-determinism (`BuilderPlannerFailed: proposed step count didn't match`). That's your own "non-deterministic rebuild" point, live. A 700-goal regeneration would (a) bake in the spurious `{path}`-style hints, (b) flake partway, (c) produce a massive churny diff — i.e. not "reproducibly green."

## The real fix (out of coder scope — routing it)
1. **Builder teaching (`.llm`)** — the LLM must not emit a `Type` on a bare `write to %var%` that has no `(kind)` hint, so `file.read`'s `{image, png}` inference isn't overridden. → **builder bot.**
2. **Determinism before any tree regeneration** — the LLM response cache (`Tests/.db/system.sqlite`) is gitignored, so CI is cold and every stale `.pr` triggers a flaky live rebuild. Either make the builder deterministic or commit/seed the cache. → **architect / infra decision.**
3. Only once (1)+(2) hold: regenerate `Tests/` and commit, then prove ≥2 consecutive clean-binary green runs with `git status` clean.

## Net
- The type-system production work (F1–F5, builder F1/F2) is sound and C#-covered (3817 green, deterministic).
- The PLang-fixture reproducibility is a **builder + determinism** problem, not a mechanical `.pr` regeneration. Flagged to builder (teaching) and architect (determinism). Coder is not blindly rebuilding 700 goals.
