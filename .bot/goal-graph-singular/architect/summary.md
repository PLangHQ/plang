# architect — goal-graph-singular

## 2026-07-22 — module⇒action ownership design + area-1b Info/Warning rulings

Two pieces this session:

**module-owns-action** ([module-owns-action.md](module-owns-action.md)) — design context, NOT yet a coder handoff (Ingi still discussing). Settled with Ingi: `action.Module` becomes the `module.@this` element (throwing getter, never nullable); ALL construction words collapse to `Create` (Mint/Load/Populate die); each owner reads exactly the one dict key it owns (collection reads `module`, module reads `name`, catalog fills); no `Handler` property anywhere — consumers' CLR-type reflection becomes action members (`Return` exists, `Capabilities`/build-validate new); pr reader gets a wire key-order contract (`module`,`name` lead) and mints through the catalog (Position comes back from the registry); `ActionName` → `Name` with the qualified face as `ToString()`.

**area-1b rulings** ([info-warning-errorlist-answer.md](info-warning-errorlist-answer.md)) — reply to coder's `to-architect-info-warning-errorlist.md`. D-A: DELETE vestigial graph `.Errors` (principle: errors are facts of a run — call frame/trail/Data; warnings are facts of a build — graph nodes). D-B: `app/warning/{this,list}` at app root, node property singular `.Warning`. D-C: graph-only pass now (`Info` survives in `Data.Result` + `BuildResponse` until the recovery-blocked area unblocks). Includes a factual correction: CallChainRenderer reads `Call.Errors` (live, populated), not graph `.Errors` — coder must not touch it.

Open: Ingi has more to discuss on module-owns-action before it goes to coder.
