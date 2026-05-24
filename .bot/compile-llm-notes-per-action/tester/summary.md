# Tester — compile-llm-notes-per-action — v2

## Version
v2 — PASS. v1 was NEEDS-FIXES (medium) on F1 (drift `.test.goal` Stale).

## What this is
v1 reported the architect-spec'd plang drift cases were committed without
`.pr` companions and showed as `[Stale]` under `plang --test` — the
load-bearing safeguard never executed. Coder v2 reshaped the goals to
work around the `.pr` MIME deserialization that defeated raw-text
asserts, then v3 (after Ingi's "you are not allowed to edit pr files")
rebuilt the drift `.pr` files via the real builder using
`--build={"files":[…]}` to scope past the `Publish.goal` wedge.

## What was done

Clean rebuild (0 errors). C# suite **2945/2945 pass** (+3 new
`DriftCaseArtifactTests`). Architect's 3-fresh-cache rule reproduced
independently with the scoping flag — all 3 rounds: 2 drift cases
`[Pass]`, 0 `[Stale]`, ~10s per round. Mutation test (inject
`"name": "channel"` into `Tests/Simple/.build/start.pr`) correctly fails
the drift case; reverted clean.

## What's still in progress / next

Nothing on the tester side. Next bot in the pipeline: **security**.

## Decisions / open items
- Fresh-build `start.pr` differs from coder v3's committed `start.pr` in
  non-load-bearing ways (LLM added `Type=string` to two `variable.set`
  calls). Drift assertions hold on both shapes. Flagging only so a
  future diff doesn't get mis-read as a regression.
- `--build={"files":[…],"cache":false}` is the scoping incantation that
  avoids the `Publish.goal` retry-path wedge. Worth knowing for any
  reviewer who needs to repeat the verification.

## v1 history (for context)
- v1 NEEDS-FIXES — F1: drift `.test.goal` Stale, no `.pr`, builder
  wedged on Publish. Closed by coder v2 + v3.
