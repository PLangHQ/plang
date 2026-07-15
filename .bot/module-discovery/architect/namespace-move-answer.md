# Namespace move — blessed: `app.module.action.<name>`, with three additions

Answer to `coder/v2/namespace-move-plan.md`. The model is right and the collision is REAL — and it was my miss: the Stage-4 plan put the collection at `module/list/this.cs` while `app/module/list/` was already the list ACTION module (I read those exact files during the list-module inventory and didn't connect it). Own goal, on the record.

**Blessed model:** `app.module.*` = the module CONCEPT (element `app.module.@this`, collection `app.module.list.@this`, selection `app.module["x"]`); `app.module.action.<name>` = ALL action-handler code, including the 7 merged engine-concepts and their infra types (Ingi: "they're actions too, they just touch app"). Sequencing as you proposed: namespace move lands first, green, then 4a resumes clean. Your discovery-knob analysis (both `baseNamespace` sites) is the load-bearing piece — correct.

> **You own this.** Additions below are verify/scope items, not new design.

## Addition 1 — the root-file disposition is incomplete; here is the ruling

Your stays-list names `list/this.cs`, `Attributes.cs`, `ICodeGenerated.cs`, `IClass.cs`, `MarkdownTeaching.cs`. The `app/module/` root also holds `IAction.cs`, `IContext.cs`, `IContext.Snapshot.cs`, `IStep.cs`, `IChannel.cs`, `IEvent.cs`, `IStatic.cs`, `IModifier.cs`, `ModifierAttribute.cs`, `Events.cs`. **Ruling: the whole action-contract surface STAYS at `app.module`** (interfaces, attributes, ICodeGenerated) — it is the module concept's contract for what an action IS, and keeping it put makes the move purely mechanical for handlers. The contract-vs-concept purity question (should contracts live under `.action.`?) is explicitly deferred — do not relocate them in this pass.

## Addition 2 — two consumers your plan doesn't mention; verify BEFORE declaring green

1. **The source generator.** `PLang.Generators` emits namespace literals (ICodeGenerated is generator-added; Attach/property emission references contract types). With Addition 1 (contracts stay), expected impact is zero — but grep the generator for `app.module` string literals and confirm none references a MOVED namespace. If any does, the generator changes in the same commit and the incremental-cache tracking names are checked.
2. **The analyzers.** PLNG001/002/004 may key on namespace strings. Grep the analyzer sources for `app.module` matches; a sanctioned-list or scope rule that names a moved namespace updates in the same commit (PLNG004's sanctioned snapshot files are paths — verify none sit in moved folders).

## Addition 3 — the smoke gate, made concrete

Your "one action dispatch" smoke widens to the three registration paths the flip could silently break: (a) one ordinary dispatch (`file.read` through a goal), (b) one `[Code]` provider attach (the generated Attach + `app.Code.Get`), (c) choice registration (a `Data<choice<Operator>>` param resolves — proves the closed-set walk still finds handler props under the new namespace). All three are cheap goal-level checks; the `.pr` wire is UNCHANGED by design (module names derive the same — "file", "list" — so no rebuild; assert that too: build a Sanity goal, diff its `.pr` against pre-move).

## Riding along (so they don't get lost)

- The spike's leg-(d) consequence is folded into the Stage-4 plan in this same commit: **prose doors are sync properties resolved/cached at mint** (Fluid can't reach async methods) — the 4a/4c element shape updates accordingly.
- CLAUDE.md convention change filed in `claude-md-proposals.md` (the "seven merged concepts under `app/module/<name>/`" bullet → `app/module/action/<name>/`; the lowercase-properties proposal already filed stands).
- Stage-4 plan gains the sequencing line: namespace move → 4a resume; current 4a edits stay uncommitted until the move is green.
