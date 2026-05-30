# docs v1 — result

## Verdict

**PASS.** Ready to merge.

## What changed

### Root CLAUDE.md (architect v1 proposal applied)

- Vocabulary list updated to lowercase **singular**: `actor`, `goal`,
  `variable`, `channel`, `error`, `event`, `filesystem`, `format`,
  `keepalive`, `snapshot`, `tester`, `type`, `config`, `callstack`,
  `data`, `module`.
- New convention bullet added: **`app.X` is the collection node, not a
  wrapper** — `app.X["name"]`, `app.X.list`, `app.X.current` (only for
  concepts execution flows through), no flat `App<Plural>` wrappers,
  registry = selection + lifecycle, behavior on the element. Module
  carve-out (no `.current`) called out.
- Token renames where the old plural surface is referenced:
  `app.types.path.**` → `app.type.path.**` (System.IO ban rule),
  `Data<app.variables.Variable>` → `Data<app.variable.Variable>`
  (PLNG001 rule), `global::app.channels.@this.Output` →
  `global::app.channel.@this.Output` (Console ban rule), property list
  `.Modules`/`.Goals` → `.Module`/`.Goal`,
  `PLang/app/modules/MarkdownTeaching.cs` →
  `PLang/app/module/MarkdownTeaching.cs`.

### `Documentation/v0.2/app-tree.md` (rewrite)

The canonical naming-dictionary doc. Major refresh:

- Vocabulary list singular.
- Top-level tree shows `Goal`, `Event`, `Module`, `Type`, `Error`,
  `Format`, `Service` (singular properties); paths point at the new
  singular folders.
- Actor surface updated: `Variables → app/variable/`,
  `Channels → app/channel/list/`.
- New section **Collection-node accessors (`app.X`)** spelling out the
  `[name]` / `.list` / `.current` convention.
- New section **Type — promoted entity behind `Data.Type`** describing
  fold-property contract and the `Promote()` throw.
- `Data` section gained a `Type` row noting non-null end-to-end +
  `type.@this.Null` sentinel.
- "What's NOT on `app`" table expanded with rows for the deleted plural
  names (`app.Goals`, `app.Modules`, `app.Channels`) pointing readers
  at their singular replacements.

### `Documentation/v0.2/scripts/check-app-tree.sh` (path fix)

Hard-coded `PLang/app/modules` → `PLang/app/module`. Drift checker
now runs and reports **clean** (30 modules, 37 app props, 7 actor
props, 7 data partials).

### Documentation/v0.2 token sweep

`architecture.md`, `action-catalog.md`, `io-channels.md`, `variables.md`,
`snapshots.md`, `code-vs-goals.md`, `object_pattern_formal.md`,
`filesystem-permission.md`, `audit/obp-rules.md`, `good_to_know.md` —
all stale `app.<plural>.X` tokens replaced with the singular form, and
where the property surface changed (`app.Module.Describe()`,
`app.channel.list.@this`) the references were corrected to current
shape rather than just naively singularised.

### `good_to_know.md` — three new sections

1. **`app.X` is the collection node — `[name]` / `.list` / `.current`** —
   the canonical accessor convention, expanding what the new CLAUDE.md
   bullet capsulises. Module carve-out, registry-vs-behavior rule,
   and the deleted-wrapper history.
2. **Producer-stamping invariant — `Data.Type` propagation** — addresses
   **auditor v1 F5**. Captures *why* the `type.@this` is stamped by the
   carrying `Data` (not at its own construction), what reads throw via
   `type.Promote()`, and the two carve-outs (primitive `_foldLoaded`,
   `ClrType` silent-null). References the three contract tests in
   `NonNullInvariantTests.cs`.
3. **`type.@this.Null` — non-null sentinel on `Data.Type`** — covers the
   three consequences callers need to know: copy-`source.Type`
   unconditional, wire skips emission, and the latent `IsNull` /
   `ReferenceEquals` footgun (codeanalyzer v4 F1, auditor v1 F2) waiting
   for a coder pass.

## What was NOT done (and why)

- **No PLang `.goal` examples written.** The rename is interior C# only.
  The tester wrote 5 PLang test contracts under `Tests/SingularNamespaces/`
  on this branch already, and the public action catalog shape did not
  change. No user-facing `.goal` documentation requires new examples.
- **No XML doc additions.** This branch added no new `public` actions
  or `Data<T>` return types. The `Promote()` throw contract on
  `type.@this` is interior runtime behaviour and is documented in
  `good_to_know.md`, not XML.
- **No CHANGELOG entry.** The rename is user-invisible — action names,
  parameter shapes, and `.pr` wire format are unchanged.

## Auditor / security follow-ups (informational — none blocking docs)

- Auditor v1 F1 (security routing gap) — security v1 ran subsequently and
  came back PASS with 3 low residuals. Routing convention now satisfied
  for the branch.
- Auditor v1 F2/F3/F4 — three latent codeanalyzer-v4 items still in HEAD
  (string-magic `IsNull`, dropped `As(string)` primitive fallback,
  `Scheme` getter null-coalesce). All non-blocking; tracked for the
  next coder pass on the relevant area.
- Auditor v1 F5 — **closed by this docs pass**: producer-stamping
  invariant now captured in `good_to_know.md`.
- Security v1 F1/F2/F3 — three low Context-null / Wire-stamp discipline
  notes; defensive only, no observed reachability. Future-work items
  for whoever next touches that code path.

## Files modified

- `CLAUDE.md`
- `Documentation/v0.2/app-tree.md`
- `Documentation/v0.2/scripts/check-app-tree.sh`
- `Documentation/v0.2/architecture.md`
- `Documentation/v0.2/action-catalog.md`
- `Documentation/v0.2/io-channels.md`
- `Documentation/v0.2/variables.md`
- `Documentation/v0.2/snapshots.md`
- `Documentation/v0.2/code-vs-goals.md`
- `Documentation/v0.2/object_pattern_formal.md`
- `Documentation/v0.2/filesystem-permission.md`
- `Documentation/v0.2/audit/obp-rules.md`
- `Documentation/v0.2/good_to_know.md`

(`Documentation/v0.2/todos.md` deliberately left alone — it's a working
file for the next pass on those areas.)
