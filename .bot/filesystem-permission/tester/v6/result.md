# tester v6 — filesystem-permission

Reviews **coder v7** (8b42b0d31, "close tester v5 F1 — pin nonce-replay half
of F-A fix"). tester v5 was NEEDS WORK with one major finding; coder v7 is a
test-only change closing it.

## Verdict: PASS — F1 closed, no new findings

coder v7 added `Scenario4_PersistedGrantReVerified_NonceReplayDoesNotReprompt`
**verbatim** from tester v5's spec. The mutation that v5 said would survive
the suite now kills the new test. Both halves of `SkipFreshnessCheck` are
independently gated.

## Suites (clean rebuild — stale-binary rule)

- C# — **2855 / 2855 pass**, 0 fail, 0 skip (+1 vs v5 = the new test).
- PLang — **203 / 203 pass** (4 intentional fail-fixtures excluded; the one
  `Failed to deserialize List`1`` stdout line is the negative-path mock test,
  unchanged from v4/v5).

## F1 — closed

The change is exactly tester v5's handed-over test, no edits:

```csharp
[Test] public async Task Scenario4_PersistedGrantReVerified_NonceReplayDoesNotReprompt()
{
    var (app1, foreignFile) = Setup("a");
    var root = app1.AbsolutePath;
    var path1 = new Path(foreignFile, app1.User.Context);
    await Assert.That((await path1.ReadText()).Success).IsTrue();   // create persisted grant

    var app2 = new global::app.@this(root);
    app2.User.Channels.Register(new StatelessChannel());
    var path2 = new Path(foreignFile, app2.User.Context);
    var read1 = await path2.ReadText();   // verify #1 — nonce cached
    var read2 = await path2.ReadText();   // verify #2 — nonce replay if step 4 active
    await Assert.That(read1.Success).IsTrue();
    await Assert.That(read1.Type?.Value).IsNotEqualTo("ask");
    await Assert.That(read2.Success).IsTrue();
    await Assert.That(read2.Type?.Value).IsNotEqualTo("ask");
}
```

## Mutation verification — the F1 claim now holds

Mutation: `permission/this.cs:147` `SkipFreshnessCheck` `true → false`, clean
rebuild, run `Scenario4*`.

| Test | Mutation result |
|---|---|
| `Scenario4_RestartStillNoPrompt_PersistedGrantSurvivesNewApp` | **pass** — one verify only |
| `Scenario4_PersistedGrantSurvivesPast_WireFreshnessWindow` | **fail** — `secondRead.Type == "ask"` (step 2, wire-freshness) |
| `Scenario4_PersistedGrantReVerified_NonceReplayDoesNotReprompt` | **fail** — `read2.Type == "ask"` (step 4, nonce-replay) |

```
failed Scenario4_PersistedGrantSurvivesPast_WireFreshnessWindow (366ms)
  Expected to not be equal to ask ... at Assert.That(secondRead.Type?.Value)
failed Scenario4_PersistedGrantReVerified_NonceReplayDoesNotReprompt (366ms)
  Expected to not be equal to ask ... at Assert.That(read2.Type?.Value)
```

One mutation, **two independent failures on different assertions** — each half
of `SkipFreshnessCheck` is now its own regression gate. This is exactly what
tester v5 said was missing. Production code restored to `true`; suite
re-confirmed green.

## Carried-over notes (non-blocking, unchanged)

- **N1** — `ValidatePathTests.UpperCasedRootPrefix_..._OnUnix` docstring still
  over-claims the `RootComparison` gate (carried from tester v4 N1). Cosmetic.
- **N4 (= auditor F-5)** — `MoveCopyBundledConsentTests` exercises bundled
  consent only on the v2 `Path` surface; the real `modules/file/copy.cs` /
  `move.cs` handlers issue two prompts. Fairly deferred with auditor's
  F-C/D/E per coder v7's out-of-scope note.

## Bottom line

coder v7 closes F1 cleanly with the exact test handed over, mutation-verified.
The F-A regression guard now covers the full mechanism — both wire-freshness
and nonce-replay. Suites green. **PASS.**
