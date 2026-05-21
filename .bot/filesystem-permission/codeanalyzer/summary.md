# codeanalyzer — filesystem-permission

## Version
v5

## What this is
Fifth pass. Reviews everything landed since v4 PASS (`3121babeb`). Only
one commit qualifies — `8b42b0d31`, coder v7, which adds a single
regression test (`Scenario4_PersistedGrantReVerified_NonceReplayDoesNotReprompt`)
in `Stage5MessagesEndToEndTests.cs` to pin the nonce-replay (step 4) half
of the F-A fix. Tester v6, security v2, auditor v2 are reports, not code.

## What was done

- Diffed v7 against the v4 baseline. **One file changed, +27 / −0, tests only.**
- Read the new test in context of its sibling `Scenario4_…_WireFreshnessWindow`
  to confirm independent coverage of Ed25519 verify steps 2 and 4.
- Applied all five passes; nothing to flag.
- Mutation-verification was already done by coder: `SkipFreshnessCheck`
  true→false kills each Scenario4 variant independently.

## Verdict: PASS

No production code changed since v4. New test is well-shaped, well-named,
mirrors the sibling. Reviewer chain stayed green through it
(tester v6 / security v2 / auditor v2).

## Carry-overs (unchanged from v4, non-blocking)

1. **Auditor F-C** — `Path.cs:125,127` / `PLangFileSystem.cs:254` still use
   `OrdinalIgnoreCase` where `RootComparison` belongs. Tracked in
   `coder/v6/result.md`.
2. **Pre-existing** — `actor/permission/this.cs` `Add` dedups by `Path`
   alone; granting a different verb on an already-granted path drops
   the prior grant.

## Code example — the new test's shape

```csharp
[Test] public async Task Scenario4_PersistedGrantReVerified_NonceReplayDoesNotReprompt()
{
    var (app1, foreignFile) = Setup("a");
    var root = app1.AbsolutePath;
    var path1 = new Path(foreignFile, app1.User.Context);
    await Assert.That((await path1.ReadText()).Success).IsTrue();

    var app2 = new global::app.@this(root);
    app2.User.Channels.Register(new StatelessChannel());
    var path2 = new Path(foreignFile, app2.User.Context);
    var read1 = await path2.ReadText();   // verify #1 — nonce cached
    var read2 = await path2.ReadText();   // verify #2 — nonce replay if step 4 active
    await Assert.That(read1.Type?.Value).IsNotEqualTo("ask");
    await Assert.That(read2.Type?.Value).IsNotEqualTo("ask");
}
```

The pairing — this test plus the existing `_WireFreshnessWindow` — means
any future regression that re-enables either step 2 or step 4 of the
Ed25519 verifier on grant verification fails exactly one of them.

## What's next

```
VERDICT: PASS
Branch is ready for docs / merge.
```
