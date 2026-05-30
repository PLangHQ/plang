# Stage 1: Rename to singular + lowercase

> Code/paths here are suggestions that pin the shape — you own the final form. See "You own this" in `plan.md`.

**Goal:** Make every `PLang/app/**` domain folder singular and lowercase, and propagate the namespace renames through both `GlobalUsings.cs`, the source generator's string literals and emitted templates, the test mirrors, and the doc references — leaving the build green and tests passing, with property names and the nullable shape unchanged.

**Scope.** Included: folder moves, `namespace`/`using` updates, alias RHS retargeting (not deletion), generator string + template updates, test inner-reference updates, doc reference updates. Excluded: any property rename (`app.Goals` stays `Goals` this stage), the accessor reshape, any nullability change, any behavior change. The four `App*` aliases are *retargeted* here, *deleted* in stage 3.

**Deliverables:** the full folder transform in `plan/rename-map.md` applied; both `GlobalUsings.cs` updated; `PLang.Generators` strings/templates updated; `PLang.Tests/App/**` inner references updated (mirror folder *names* unchanged); `Documentation/**` references updated; a `claude-md-proposals.md` entry for the repo `CLAUDE.md` OBP-block tokens (do not edit CLAUDE.md directly); clean rebuild + both test suites green.

**Dependencies:** None. This is the foundation.

## Design

Work **one subsystem at a time**, not the whole tree at once. The aliases shield consumers, so each subsystem is independently recoverable: move its folder → fix its `namespace` decl → fix that subsystem's alias RHS in both `GlobalUsings.cs` → fix direct `global::app.<old>` references → fix the generator strings for that namespace → rebuild green → next. Suggested order (leaf-light to heavy): `format`, `error`, `event`, `variable`, `channel`, `goal`, `type`, `module`. The PascalCase fixes (`builder/type`, `tester/test`, `http/response`, `mock`, `service`) and the `callstack/call/*` singulars can interleave wherever convenient — small blast radius.

The collapses do real structural work, not just a move: `goals/this.cs` (registry) lands at `goal/list/this.cs` and `goals/goal/this.cs` (element) lands at `goal/this.cs`. After this stage the *types* are already `goal.@this` (element) and `goal.list.@this` (registry) — the property `app.Goals` simply returns the renamed `goal.list.@this`. Stage 3 renames the property and adds the selectors. Same pattern for channel and the serializer subtree.

**Type is the one asymmetry, and its subtree grew.** The plang-types merge filled `app/types/` out to ~55 files / ~20 subdirs (value types `number/`/`image/`/`code/`/`datetime/`/`duration/`/`path/**`; machinery `kinds/`/`renderers/`/`primitives/`; partials `Registry.cs`/`Conversion.cs`/`Loader.cs`). All of it rides along below `type/` (already lowercase — apply the singular rule to `kinds`/`renderers`/`primitives`). The registry `types/this.cs` (+ its partials) collapses to `type/list/this.cs` like the others — but the *element* (`type/this.cs`) does **not** appear here, because the type entity currently lives at `app.data.type`. Moving it into `type/this.cs` is Stage 4. So after Stage 1, `type/` has `list/` + value-type folders, no `type/this.cs`. See `plan/rename-map.md` and `plan/type-entity.md`.

**Module is a plain rename here:** `modules/` → `module/`, and `modules/this.cs` → `module/this.cs` (= `module.@this`, the action registry — *not* a `registry.cs`, the demote was dropped). No collapse, no structural change in this stage; `app.Modules` keeps returning it under the new namespace. The per-module `types.cs` → `type/` folders (see `rename-map.md`).

**The generator is the trap.** `PLang.Generators` references namespaces as **string literals** and inside **emitted-code templates** — the compiler does not check them, so a miss compiles clean in the generator and breaks at *consumer* compile time with shapes like `Action '<module>.<action>' not found` or missing-type errors. The critical ones: `Emission/Action/this.cs:177` (`const string prefix = "app.modules."` → `"app.module."`), the emitted namespace literals in that file's templates, and `Discovery/this.cs` string predicates (`"app.modules"` → `"app.module"`; leave `"app.data"`). Full list in `plan/rename-map.md`. **Verify by clean rebuild** (`rm -rf` the bin/obj per the stale-binary trap in `/CLAUDE.md`), never by assuming.

**Two brief corrections** (see `plan/rename-map.md`): `Attributes/`, `Diagnostics/`, `Statics/`, `Utils/` stay PascalCase (C# infra, not domain) — so the `AppStatics` alias RHS does *not* change. `serializers/` collapses to singular `serializer/` and `channels/channel/events/` → `channel/event/`.

**Do not delete `.build` folders.** Tracked `.pr` files live there. `plang --test` runs off a pre-built binary — rebuild clean before trusting any PLang test result.
