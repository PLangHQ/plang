# docs summary — singular-namespaces

**Version:** v1

## What this is

Final documentation gate on the `singular-namespaces` branch — a 4-stage
refactor that (1) renamed `app/<plural>` folders to `app/<singular>`
lowercase, (2) made `app` / `context` / `data._context` non-null,
(3) reshaped `app.X` into the collection-node accessor (`[name]` /
`.list` / `.current`) deleting the `App<Plural>` wrapper aliases, and
(4) promoted `type.@this` to a real entity behind `Data.Type` with fold
properties and a `Promote()` throw on unstamped non-primitive reads.

The branch chain landed PASS at every verdict (codeanalyzer v4, tester
v3, auditor v1+v2, security v1) but touched **no Documentation/ files**.
Docs v1 closes that gap.

## What was done

### Applied architect's root CLAUDE.md proposal

Lowercase vocabulary list updated to singular; new canonical convention
bullet added describing `app.X` as the collection node; stale token
references through CLAUDE.md fixed (`app.types.path.**` →
`app.type.path.**`, `Data<app.variables.Variable>` →
`Data<app.variable.Variable>`, `global::app.channels.@this.Output` →
`global::app.channel.@this.Output`, property-list `.Modules`/`.Goals` →
`.Module`/`.Goal`).

### Rewrote `Documentation/v0.2/app-tree.md`

The canonical app-graph dictionary. Singular vocabulary, current
property names, current folder paths, plus new sections on the
collection-node accessor convention, the promoted `Type` entity, and
the `Null` type sentinel. Drift checker (`check-app-tree.sh`) updated
for the new path and verified **clean**.

### Swept stale plural namespace tokens

Across 9 files in `Documentation/v0.2/`: `architecture.md`,
`action-catalog.md`, `io-channels.md`, `variables.md`, `snapshots.md`,
`code-vs-goals.md`, `object_pattern_formal.md`,
`filesystem-permission.md`, `audit/obp-rules.md`, `good_to_know.md`.

### Wrote three new `good_to_know.md` sections

1. **`app.X` is the collection node — `[name]` / `.list` / `.current`**
   — full accessor convention with module carve-out.
2. **Producer-stamping invariant — `Data.Type` propagation** — closes
   auditor v1 F5. Captures the rule that `type.@this.Context` is
   propagated by the *Data* setter, not by the type's own ctor, plus
   the `Promote()` throw contract and the two carve-outs.
3. **`type.@this.Null` — non-null sentinel on `Data.Type`** — the
   non-null end-to-end contract, copy-unconditional discipline, wire
   skip-emission rule, and the latent string-magic footgun (waiting on
   a future coder pass to switch to `ReferenceEquals`).

## Code example — the new accessor pattern, applied to docs

The convention the docs now teach:

```csharp
// Old (deleted):
//   App.Goals.GetByPath("main")
//   App.Channels.Get(name)
//   App.Modules.Describe()

// New (canonical):
app.Goal["main"]              // select one — throws on miss
app.Goal.list                 // enumerate
app.Goal.current              // currently-executing goal
app.CurrentActor.Channels[name]   // per-actor (channel lives on actor)
app.Module.Describe()         // module has no .current — it's dispatch-only
```

Three deleted wrappers (`AppGoals`, `AppChannels`, `AppEvents`,
`AppModules`) were exactly the "collection lives next to the element
as a flat property" smell. The doc now states this directly under both
the CLAUDE.md convention bullet and the new good_to_know.md section.

## Verdict

**PASS.** Ready to merge. Next: `git merge singular-namespaces` into
`runtime2`.
