# Open threads + settled rejections

## Deferred — don't block shipping

1. **Per-channel signing opt-out.** Currently every Data IO signs (see [transparent-signing.md](transparent-signing.md)). If a real perf case shows up (high-volume debug logs, stderr spam), add per-channel opt-out flags. Not now.
2. **Key rotation.** PLang's `IKeyProvider` has a single key today. If the System actor's signing key rotates, outstanding callbacks signed with the old key fail verification. Address with a key-ring (current key for new signing, recent keys accepted for verification). Ships with single-key, single-version. Documented as known limitation.
3. **Error code surface for `goal_hash` mismatch.** Hard error is settled (see [resume-mechanics.md](resume-mechanics.md)); the actual error code (`CallbackGoalHashMismatch`?), error message format, and PLang `on error` handling shape are coder/test-designer-level decisions.
4. **`App._statics` TODO closure.** When the goal-backed dynamic property replacement lands, drop `Statics` from the `Callback` schema.
5. **Module-behavior drift not in `Goal.Hash`.** Documented in [resume-mechanics.md](resume-mechanics.md#goalhash-subtlety--known-limitation). Could extend hash on a future branch if needed.
6. **`-run %callback%` action shape.** Lives in a `callback` module presumably (`callback.run` action). Builder annotation, parameter naming, return shape — coder/test-designer call.
7. **Loading a Callback from a non-Data wrapper.** If a developer reads from a backend that returns plain JSON (not Data-wrapped), how does `- run` reconstitute the `Data<Callback>` envelope and verify? Probably the deserializer auto-wraps when reading typed `Data<T>`, but worth confirming.
8. **Storage shape recommendations for error-retry.** No runtime opinion; developer chooses via PLang (file, DB provider, queue). Built-in `callback.store` / `callback.load` ergonomics rejected for now (see below) — revisit if a real ergonomics case appears.
9. **Ask-user builder annotation** — exact `.pr.json` shape for `vars: %x%, %y%` on a step. Coder/builder concern.
10. **Resumed-call signal for action handlers.** The exact mechanism by which an `ask user` handler distinguishes "fresh call, issue Callback" from "resumed call, return bound value." Likely a flag on the directive readable through `Context`. Coder-level decision.

## Settled rejections — design-specific

(Cross-topic rejections live in `plan.md`. These are specific to topics here.)

- **Explicit `- sign %callback%` at every issue site.** Rejected. Default signing is transparent at the IO boundary; developers only call `- sign` explicitly when they want to override (custom expiry, contracts, headers).
- **`Expiry` field on `Callback` record.** Rejected. Lives in `Data.Signature` (the renamed `SignedData`). One source of truth. See [callback-schema.md](callback-schema.md).
- **Step-level `vars:` capture annotation.** Only on ask-family actions. Error-retry doesn't need declared vars — it captures everything. See [plang-surfaces.md](plang-surfaces.md).
- **Re-running producer steps for stateful provider re-acquisition.** Not now. Built-in providers are pure or wrap external state (no inter-action mutable state). Third parties opt in via `ISnapshotted`. Event-sourcing is a separate, larger conversation.
- **Resumer's identity replacing captured identity.** The signed envelope authorises; the captured `ActorName` + `Identity.Name` dictate what the resumed code runs as. Who triggers `- run %callback%` is independent of what privilege the resumed code holds.
- **Callstack-walking as the cache audit substrate.** Rejected on two grounds: Calls pop on completion (forcing `Flags.History` on universally would be too expensive), and cache snapshotting is rejected entirely as a feature. The pattern of "subsystem owns its own audit-after-pop tracking" remains valid for any future subsystem that needs it.
