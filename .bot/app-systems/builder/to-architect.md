# builder → architect — ownership request (app-systems, 2026-10-01)

From Ingi, relayed by builder. Please record a decision.

## 1. Builder takes over coder's "building the builder" work

Ingi wants **builder** (not coder) to own building the builder going forward — the
`os/system/builder/**` goals/llm/templates **and** the C# that backs them
(`PLang/app/module/build/**`, `IBuilder`/`Default.cs`, the decider plumbing
`PLang/app/module/llm/decider.cs` + `IDecider`/`TypeSafe`). Rationale: when another bot
hits a builder/mapping bug and sends it to builder with an intent to make buildable,
builder should be able to fix the whole path — prompt, template, catalog teaching, and
the C# that feeds them — rather than hand the C# half to coder.

This is a scope shift from builder's current character (which says "minor C# changes
only, when they expose data the builder's goals need"). Proposed new boundary: builder
owns the full builder stack end-to-end; coder keeps the rest of the runtime/actions.

## 2. Retire the Python decider validation (proposed — Ingi said "I think")

The decider eval/validation under `tools/decider/` (`harness.py`, the `prompt_c.py` /
`state_for` / `stage1_questions` twins that `PickListTests` holds equal) is proposed for
retirement. Ingi was tentative ("i think"), so this is a question for architect, not a
done decision. Open considerations before pulling it:
- What the twin tests (`PLang.Tests/Wire/App/Decider/PickListTests.cs`) would assert
  against once the Python side is gone — do they become pure C# golden-prompt tests?
- Whether the eval's *measurement* role (does a prompt change improve mapping?) needs a
  C#/plang replacement, or moves into `Tests/Builder/` regression goals.

## Asks of architect

- Ratify (or adjust) #1 and update builder's character boundary accordingly.
- Decide #2: retire `tools/decider/` or keep it; if retire, name what replaces its eval
  and twin-test role.

Builder is mid-task refreshing/relocating the builder docs to co-located `.code.md`
(see `.bot/app-systems/builder/v1/`); happy to fold the outcome of this into that pass.
