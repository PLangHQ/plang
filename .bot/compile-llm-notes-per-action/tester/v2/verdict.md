# Tester verdict — compile-llm-notes-per-action — v2

**Status:** PASS

## What changed since v1

Coder v2 (`a7c8beaca`) addressed F1 by making the drift `.test.goal` files
copy `start.pr` → a local `.txt` and assert on raw text (sidestepping the
`.pr` MIME → Goal-object deserialization that defeated substring asserts),
plus adding 3 `DriftCaseArtifactTests.cs` as the C# structural mirror.

Coder v3 (`8784c6c52`) responded to Ingi's "you are not allowed to edit pr
files" by rebuilding the drift `.pr` files via the real builder using
`--build={"files":["Tests/Simple/Start.goal"],"cache":false}` — the
scoping flag that avoids the `Publish.goal` wedge tester v1 reported.
Goals updated with `, overwrite` on the copy step and unique destinations
(`start-out.txt`, `start-eq.txt`) so concurrent runs don't collide.

## Verification

**Clean rebuild from zero** — 0 errors. `dotnet build PlangConsole` + `dotnet build PLang.Tests`.

**C# suite** — 2945/2945 pass (was 2942; +3 = `DriftCaseArtifactTests`).
`DriftCaseArtifactTests` 3/3 pass.

**Architect's 3-fresh-cache rule, reproduced independently:**

```
for i in 1 2 3; do
  rm -rf Tests/Simple/.build
  plang build '--build={"files":["Tests/Simple/Start.goal"],"cache":false}'
  cd Tests && plang --test Builder/CompileLlmNotes
done
```

| Round | Build time | output-write-no-channel | assert-equals-no-message |
|-------|------------|--------------------------|---------------------------|
| 1     | 11.0s      | Pass (30ms)              | Pass (2ms)                |
| 2     | 10.3s      | Pass (13ms)              | Pass (32ms)               |
| 3     | 10.1s      | Pass (15ms)              | Pass (4ms)                |

No Stale entries. Both drift cases execute and pass on every round.

**Mutation test for the drift cases.** Injected the forbidden shape
(`"name": "Data"` → `"name": "channel"`) into `Tests/Simple/.build/start.pr`
and re-ran `output-write-no-channel.test.goal`:

```
[Fail] Builder/CompileLlmNotes/output-write-no-channel.test.goal (61ms)
```

Reverted to HEAD; suite back to green. The drift case actually pins the rule
it claims to pin — not green-by-construction.

**Observation (non-blocking).** When I rebuilt `Tests/Simple/Start.goal`
fresh-cache, the LLM emitted slightly different shapes than what coder v3
committed: my run added a `Type=string` parameter to `variable.set` for two
of the three `set` steps, and flipped one `Value`'s declared type from
`tstring` → `string`. Neither difference affects the drift assertions
(channel/Message absence holds on both shapes). This is real LLM variation
within the unconstrained-by-architect surface — fine. Worth flagging only
so a future reviewer who diffs `start.pr` after their own rebuild knows it
isn't a regression.

## Findings

None.

## What v3 should look like

No further tester pass needed unless coder/architect change the contract.
Next bot: **security** (or whatever the pipeline schedules next).
