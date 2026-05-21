# codeanalyzer v5 — filesystem-permission

## Scope

One commit since v4 PASS: `8b42b0d31` — coder v7. Pure test addition
(`Stage5MessagesEndToEndTests.cs` +27 / −0) closing tester v5 F1. No
production C# changed on the branch since v4.

## PLang.Tests/App/FileSystem/Stage5MessagesEndToEndTests.cs

### OBP Violations
None. Test file; OBP shape rules don't apply, and the test itself touches
no public collection, no cross-file lock, no split lifecycle.

### Simplifications
None. The test is 18 lines of body, every line earns its place:

- `Setup("a")` — single canned answer; the second read should NOT consume it.
- Two reads against one `Path` — pairs against `Scenario4_…_WireFreshnessWindow`
  which only exercises step 2 (wire-freshness). This one exercises step 4
  (nonce-replay) by reading twice within the freshness window.
- Asserts both `Success == true` and `Type?.Value != "ask"`. The latter is
  the prompt-fired sentinel used consistently across the file.

Deletion test (Pass 5): removing any single line breaks the regression
coverage. Removing `read2`'s assertions reduces it to the existing
Scenario4. Mutation already verified by coder (`SkipFreshnessCheck`
true→false kills it independently of the sibling).

### Readability
1. **Line 155-161: doc comment** — earns its place. Says *which* of the
   five Ed25519 steps this guards (step 4), why a fresh `Data` per `Find`
   matters (SettingsStore.GetAll), and explicitly pairs with the sibling
   Scenario4. Future reviewers won't have to reconstruct intent.
2. **Lines 174-175 inline `// verify #1 — nonce cached` / `// verify #2 — nonce replay if step 4 active`**
   — non-obvious WHY (what would go wrong without the fix). Earned.

Naming: `Scenario4_PersistedGrantReVerified_NonceReplayDoesNotReprompt`
mirrors the file's "Scenario<N>_<Behavior>_<Outcome>" convention.

### Behavioral notes
- Uses `new StatelessChannel()` for app2 → stateless channel, so a re-prompt
  *would* return an unhandled `ask` bubble and the `Type != "ask"` assertion
  would catch it. (Compare Scenario4_WireFreshness which uses a probe
  with zero "a" answers — same fail mode.)
- No `NowUtc` advance: deliberate — step 4 fires inside the freshness window
  on the second pass, which is the whole point. Pairing the two tests gives
  independent coverage of step 2 and step 4 so a partial regression on
  `SkipFreshnessCheck` (e.g. switching to a "skip only freshness" variant)
  would fail exactly one of them.

### Verdict: CLEAN
Test is shaped right, doc-commented for the next reviewer, mutation-verified
by coder. Mirrors its sibling. Nothing to fix.

---

## Branch-wide

No new production code since v4. The four-file analysis from v4 still stands.
The two carry-overs called out there (auditor F-C `RootComparison` and the
`actor/permission Add` Path-only dedup) remain non-blocking and tracked in
coder/v6/result.md.

### Auditor v2 F1 — codeanalyzer self-correction

The auditor v2 report names a doc-comment defect codeanalyzer v4 missed:

- **File:line** — `PLang/app/actor/permission/this.cs:11-13`
- **Current code**:
  ```csharp
  ///   - <b>Session ("y")</b> — no expiry on signature, lives in an in-memory
  ///     list, dies when the App exits.
  ///   - <b>Persisted ("a")</b> — signature has an expiry, routed to
  ///     <c>app.SettingsStore</c> under the <c>permission</c> table.
  ```
- **Correct form** — after F-A the polarity is inverted: persisted "a" grants
  are signed with `Expires == null` (no signature expiry — they are the
  permanent ones), and session "y" grants are unsigned. The F-A remediation
  fixed the equivalent doc in `filesystem/permission/this.cs:22-26` but left
  this sibling lying.
- **Why I missed it** — v4 read the v6 diff plus the four touched files. The
  auditor read every file describing the same concept. Confirmed at HEAD:
  the comment is still wrong.
- **Why it matters** — doc-only, but the docs bot is the next pipeline stage
  and may propagate the inversion into user-facing material. Trivially
  fixable (rewrite the two bullets). Left for the coder per scope rules.

Captured as Learning #1 in `/learnings/filesystem-permission/codeanalyzer/v5/learnings.md`.

## Verdict: PASS (with one trivial doc-fix flagged for the coder)

Coder v7 lands a clean test; no production drift since v4; reviewer chain
(tester v6 / security v2 / auditor v2) is green. The auditor F1
doc-inversion in `actor/permission/this.cs:11-13` is a 4-line fix that
the coder should land before docs propagation, but it does not block
the branch (the code is correct; only its self-description is wrong).

```
VERDICT: PASS
Next: coder closes auditor v2 F1 (4-line doc fix in actor/permission/this.cs),
      then branch advances to docs / merge.
```
