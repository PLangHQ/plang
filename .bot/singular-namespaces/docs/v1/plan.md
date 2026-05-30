# docs v1 — singular-namespaces

## Context

4-stage refactor: (1) plural→singular+lowercase rename across `app/**`;
(2) `app`/`context`/`data._context` non-null; (3) `app.X` collection-node
accessor (`[name]` / `.list` / `.current`, no more `App<Plural>`
wrappers); (4) `type.@this` promoted to entity with fold properties +
`Promote()` throw on unstamped non-primitive reads. Verdict chain on
this branch: codeanalyzer v4 PASS → tester v3 PASS → auditor v1 PASS
→ security v1 PASS. Only 3 low security residuals + 4 minor auditor
nits — none blocking.

## Scope of the docs pass

**No `Documentation/` or `docs/` files were touched on this branch.** The
rename + accessor reshape silently invalidated several references. The
docs gap is the entire doc surface for the singular world.

### Stale references (token rename)

Plural → singular sweep across:

- `CLAUDE.md` (root) — `app.channels.@this.Output` × N,
  `Data<app.variables.Variable>`, `app.types.path.**` (System.IO ban
  rule), the lowercase-vocabulary list itself.
- `Documentation/v0.2/architecture.md` — `app.modules.{module}`,
  `app.modules.Describe()`, `Data<app.variables.Variable>`,
  `modules.variable` example.
- `Documentation/v0.2/action-catalog.md` — `app.modules.Describe`,
  `Data<app.variables.Variable>`, `namespace app.modules.variable`.
- `Documentation/v0.2/io-channels.md` — `app.channels.@this` ×
  several, `AppChannels`, `app.modules.debug`.
- `Documentation/v0.2/variables.md` — `app.variables.@this`.
- `Documentation/v0.2/snapshots.md` — `app.variables.@this`,
  `app.modules.code.@this`, `app.modules.builder.@this`.
- `Documentation/v0.2/code-vs-goals.md` — `app.modules.code` × many.
- `Documentation/v0.2/object_pattern_formal.md` — `app.modules.{module}`.
- `Documentation/v0.2/filesystem-permission.md` —
  `app.modules.signing.verify`.
- `Documentation/v0.2/audit/obp-rules.md` — `app.modules.Schema.Build`,
  `Catalog.@this.Build(action.Context.App.Modules)` (note: `App.Modules`
  on the property is **still PascalCase** — only the namespace went
  singular, not the property surface; verify before bulk-renaming).
- `Documentation/v0.2/good_to_know.md` — many: `app.variables.@this`,
  `app.types.path`, `Variables = app.variables.@this`, `AppGoals`,
  `AppChannels`, `app.channels.@this.Output`.

### New canonical material to write

1. **`app.X` accessor model** — new section in `good_to_know.md`:
   collection-node convention, `[name]`/`.list`/`.current` rule, the
   "no `App<Plural>` wrappers" smell, `module` carve-out (no `.current`).
2. **Producer-Stamping Invariant** (auditor F5) — new section in
   `good_to_know.md`: `Data` ctor sets `_type` directly; `type.Context`
   propagates only via `Data.Context` setter; reading fold properties
   (`Fields`/`Values`/`Example`/etc.) before the Data is stamped throws
   via `type.Promote()`. Defense-in-depth proven by
   `TypeFoldRead_OnUnstampedDomainEntity_ThrowsHard`.
3. **`Null` type sentinel** — short note alongside producer-stamping:
   `Data.Type` is non-null end-to-end; unset → `type.@this.Null`
   (`IsNull = true`, `ClrType = typeof(object)`); Wire skips emission;
   the `Type` setter clears `_type` on Null assignment so call-sites
   can copy `source.Type` unconditionally.

### Root CLAUDE.md proposal (architect — v1)

Architect's proposal stands. Apply on merge:

- Token renames listed above.
- New convention block describing `app.X` as collection-node with
  `[name]` / `.list` / `.current`.
- Update the lowercase-vocabulary list to singular.

Decision: **apply** (verbatim, with the minor cleanup that `App.Modules`
property stays PascalCase — only the *namespace* `app.module` went
lowercase singular).

## What I will NOT do

- **No PLang `.goal` examples for new accessor surface.** The tester
  already wrote 5 PLang test contracts (`Tests/SingularNamespaces/**`)
  and a `ChannelWriteThroughAccessor` Capture goal. No user-facing
  module documentation requires new `.goal` examples for this rename —
  the public action catalog is unchanged in shape (the rename is interior
  C# only).
- **No XML doc additions.** This branch is a rename + structural reshape;
  no new `public` actions or `Data<T>` return types were added. The
  `Promote()` throw contract on `type.@this` is interior runtime
  behaviour — documented in `good_to_know.md`, not XML.
- **No CHANGELOG entry inside `v1/result.md`** beyond a one-liner — the
  rename is user-invisible. Action catalogs do not change.

## Plan execution order

1. Apply architect's root CLAUDE.md proposal (rename tokens + accessor
   convention).
2. Sweep Documentation/v0.2/ stale tokens with targeted edits.
3. Write the three new sections in `good_to_know.md` (accessor model,
   producer-stamping invariant, Null sentinel).
4. Write `docs-report.json`, `verdict.json`, `summary.md`.
5. Commit + push.

## CLAUDE.md / character proposals (decisions table)

| From | Target | Decision | Reason |
|---|---|---|---|
| architect v1 | /CLAUDE.md | applied | Singular tokens are now ground truth; accessor convention is canonical for all future `app/` work; lowercase-vocabulary list must match the tree |

No character proposals filed on this branch.
