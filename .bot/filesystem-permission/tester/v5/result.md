# tester v5 — filesystem-permission

Reviews **coder v6** (894d6a0ca, "close auditor F-A — persistent-grant
durability") on top of the app-lowercase merge. tester v4 PASSed v4+v5;
the auditor then FAILed the branch on F-A and rated my v4 *partial* —
Scenario4 never advanced `NowUtc`, so durability was unverified.

## Verdict: NEEDS WORK — 1 finding (major, test-coverage)

The production fix is **correct** — verified working by mutation. But coder
v6's `SkipFreshnessCheck` flag neutralises **two** independent checks, and
only **one** of them has a test behind it. A regression in the other half
survives the full 2854-test suite.

## Suites (clean rebuild — stale-binary rule)

- C# — **2854 / 2854 pass**, 0 fail, 0 skip (+1 vs v4 = the new durability test).
- PLang — **203 / 203 pass** (4 intentional fail-fixtures excluded; the one
  `Failed to deserialize List`1`` stdout line is the negative-path mock test,
  unchanged from v4). The app-lowercase merge left both suites green.

## What coder v6 changed

`signing.verify` gained `SkipFreshnessCheck`. When true, `Ed25519.VerifyAsync`
skips **two** steps:
- **Step 2 — wire-freshness**: `now - Created > TimeoutMs` (default 5 min) → `TimedOut`.
- **Step 4 — nonce-replay**: the signature's nonce is cached for `TimeoutMs`;
  a second presentation → `NonceReplay`.

`Permission.VerifySignature` passes `SkipFreshnessCheck=true` so grants live
by their `Expires` field alone (null = permanent today).

## Mutation verification

Mutation: `permission/this.cs:147` `SkipFreshnessCheck` `true → false`, clean
rebuild, full C# suite.

| Result | Tests failed |
|---|---|
| Mutation applied | **1** — `Scenario4_PersistedGrantSurvivesPast_WireFreshnessWindow` |

coder v6's commit message claims *"Mutation-verified: revert
SkipFreshnessCheck=true→false → test fails."* That is literally true — but the
mutation flips **both** checks at once, and only the **step-2** half is caught.
The new test advances `NowUtc` +10 min, so it dies on step 2's age check. It
never triggers step 4: app2 has a fresh cache, the grant is verified exactly
once there, so no nonce is ever re-presented.

### F1 (major) — the nonce-replay half of the F-A fix is ungated

**No test verifies a persisted grant twice.** `VerifySignature` is cached
per-`Data`-instance via the `VerifiedFlag` property (`permission/this.cs:126`),
but persisted `Find` re-deserializes a fresh `Data` on **every** call
(`permission/this.cs:55`, `SettingsStore.GetAll`). So two `Find`s on a
persisted grant = two real `VerifySignature` calls = two `Ed25519` passes.
With step 4 active, the second pass hits `NonceReplay` and the user is
re-prompted — exactly the F-A symptom ("always allow" stops covering), just
triggered by re-reads inside one app rather than by the wall clock.

No existing test does two verifications of a persisted grant:
- `Scenario3_ImmediateRereadSkipsPrompt` reads twice in one app, but grant
  *creation* (read 1) does not verify — only read 2 verifies. One pass.
- `Scenario4_RestartStillNoPrompt` / the new `..._WireFreshnessWindow`:
  app1 creates (no verify), app2 reads once. One pass.

**Confirmed by experiment.** I added a scratch probe — app1 grants "a"
(persisted), app2 reads the foreign file **twice** with a stateless channel
(a re-prompt surfaces as `Type == "ask"` instead of hanging):

```
SkipFreshnessCheck = false (mutation):  read2.Type == "ask"  → FAIL (re-prompt)
SkipFreshnessCheck = true  (the fix):   read2 covered         → PASS
```

So a regression that re-enabled step 4 for grants — leaving step 2 skipped —
would pass the entire suite today. The headline feature would silently
half-break: any app that reads the same foreign resource more than once
(e.g. a Messages app polling Email's `system.sqlite`) re-prompts on the
second read. Scratch probe deleted after the experiment; tree is clean.

**Fix — add this test** (drop into `Stage5MessagesEndToEndTests.cs`; the
`StatelessChannel` helper already exists in that file):

```csharp
/// Auditor v1 F-A, nonce-replay half: a persisted grant is re-verified on
/// every Find (SettingsStore.GetAll yields a fresh Data each call, so the
/// per-instance VerifiedFlag cache does not carry across reads). Without
/// SkipFreshnessCheck neutralising step 4, the second verification inside
/// one app would hit NonceReplay and re-prompt. Pairs with
/// Scenario4_PersistedGrantSurvivesPast_WireFreshnessWindow, which gates
/// only step 2 (wire-freshness).
[Test] public async Task Scenario4_PersistedGrantReVerified_NonceReplayDoesNotReprompt()
{
    var (app1, foreignFile) = Setup("a");
    var root = app1.AbsolutePath;
    var path1 = new Path(foreignFile, app1.User.Context);
    await Assert.That((await path1.ReadText()).Success).IsTrue();   // create persisted grant

    // app2: two reads. Each Find re-deserializes the grant → two real
    // VerifySignature passes → step 4 would NonceReplay the second.
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

Mutation-verified by me: this shape fails under `SkipFreshnessCheck=false`
(`read2.Type == "ask"`) and passes with the fix in place.

## Carried-over notes (non-blocking, unchanged from v4 + auditor)

- **N1** — `ValidatePathTests.UpperCasedRootPrefix_..._OnUnix` docstring still
  over-claims the `RootComparison` gate (tester v4 N1). Untouched by v6.
- **N4 (= auditor F-5, attributed to tester)** — `MoveCopyBundledConsentTests`
  exercises bundled consent only on the v2 `Path.MoveTo/CopyTo` surface; the
  real `modules/file/copy.cs` / `move.cs` handlers issue two prompts
  (documented v1 degradation). The test name implies coverage of a UX the
  shipped handler path does not deliver. Add a one-line note in the test, or
  a handler-path two-prompt test. Minor; fair to defer with F-C/D/E.

## Bottom line

coder v6's fix is real and the suite is green, but the F-A regression guard
covers only half the mechanism it claims to. One added test (above) closes
F1 and makes the mutation claim in the v6 commit message actually hold.
Re-issue as a quick v7.
