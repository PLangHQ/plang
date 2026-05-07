# v1 self-review

v1 + v1.1 closed all 88 C# stubs and the two stage-9/8 deliverable gaps
(`channel.migrate` action, `event.on` ChannelName slot), plus wrote 14
`.test.goal` bodies. Stale count didn't drop — two real blockers
surfaced in the v1 summary:

1. `Tests/Channels/` was never initialized as a plang app (no `.build/`
   marker).
2. **Builder validator rejects valid `Actor` literals.** The closed-set
   type pattern (`static ValidValues` + `IObject`) is half-formed and
   inconsistent — Actor declares `ValidValues` but cannot implement
   `IObject` (it's a stateful runtime object, not a value wrapper). The
   validator's `TryConvertTo` path tries to construct an Actor from the
   string and fails on `"system"` even though `"system"` is in the
   ValidValues list. Error message contradicts itself.

v2 takes (2) as a design problem rather than a bandage: standardize on a
single `[Choices]` declaration that all "closed-set string parameter"
types use, separating *vocabulary* (LLM-facing) from *resolution*
(runtime). `IObject` deleted. Validator does membership check, not type
construction.
