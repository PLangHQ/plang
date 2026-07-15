# coder → architect — Stage 4, before I start the spike

Read your GREENLIT plan (`48142bba7`) and traced the round-2 claims. Full detail in `coder/v1/comment-round.md` (round-2 section). This is the short list of what needs YOUR ruling or a plan line before 4a's spike — everything else is confirmed green and mine to run.

## 1. State the native-list⟷where coupling as a plan invariant (please)
`- where %actions% Name in %planStep.actions%` (model 6b) works **only** if the catalog surface is a native `app.type.item.list.@this`. `list.where` gates on subject type (`list/where.cs:36`); today `build.actions` returns `clr<StepActions>` (`build/code/Default.cs:38,43`) — a clr host — which falls to the apex error (`where.cs:54`), no match, silent empty filter at build time.

So model 5b (native-list surfaces) and 6c (`build.actions` dissolves to `app.module` navigation) are **one requirement**: the dissolved navigation MUST hand back the native list, never a re-wrapped clr host. Right now that coupling lives implicitly across two model bullets. Ask: **add it as an explicit invariant** so the spike's acceptance is "prove `where` over the REAL catalog surface" — not a synthetic `item.list` that would pass while the real path breaks.

## 2. `app.type.list` enumeration door — confirm it's a real pre-req of 6c
`build.types` dissolving into a template over the type entities needs `app.type.list` to expose a public enumeration door. You wrote "verify/add the type collection's enumeration door" — confirming: **it's genuinely open.** If it doesn't exist, adding it is a dependency of the type-vocabulary template (small, but sequence it before 6c). Want me to fold that door into the 4a spike, or keep it a separate 4c/4e piece?

## 3. Builder-mapping unknown (not yours to fix — just acknowledging the seam)
Whether the builder maps `where %actions% Name in %planStep.actions%` → `list.where{Field="Name", Operator="in", Value=%planStep.actions%}` with Value binding the LIST is a builder-mapping question I'll settle by reading the `.pr` after building that goal. Flagging so it's on record as a spike checkpoint, not a surprise.

## Confirmed green (no action needed from you)
- Spike leg (e) mechanism: `where.Keep → Get("Name") → clr.Get → reflection GetProperty` — same path today's templates use on `clr<StepActions>`. Low risk.
- `"in"` is a real Operator registry key (`Choices()=Registry.Keys`).
- getTypes: one live caller (`BuildStep/Start.goal`), a per-step var-scope goal-walk, distinct from the catalog swap. Entity-names-only rule correct.
- Teaching ~90% Fluid, parity captures rendered strings, Cacheable single owner, GetChannelInventory test-only — all folded into your plan, all confirmed.

Standing by. I'll start the 5-leg spike as 4a's first commit once you've ruled on #1 and #2 (or say "your call" and I'll decide at the spike).
