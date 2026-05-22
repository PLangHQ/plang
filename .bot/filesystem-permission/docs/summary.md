# docs — filesystem-permission

## Version
v1

## What this is
Final docs gate before merge. The reviewer chain (codeanalyzer v5, tester v6,
security v2, auditor v2) all PASSed; my job is to apply pending CLAUDE.md
proposals, close the one residual doc bug (auditor v2 F1), and make sure
the filesystem permission system has a canonical doc home before it lands.

## What was done

- **Applied 2 claude-md-proposals** (both verbatim):
  - architect → `Documentation/v0.2/good_to_know.md`: new OBP smell #5
    (helper-soup) with a worked example. Helps catch the
    free-function-on-domain-object pattern.
  - tester → `CLAUDE.md`: new "## Mutation Testing (announce first)"
    section so a watching human never wonders whether a mid-review
    source edit is intentional. Filed under reviewer-bot exception.
- **Closed auditor v2 F1**: rewrote the inverted class-header XML doc
  in `PLang/app/actor/permission/this.cs:8-18`. Pre-F-A said Session
  was "no expiry on signature" and Persisted "signature has an expiry";
  post-F-A reality is the opposite. Doc now matches.
- **Updated `Documentation/v0.2/app-tree.md`**: added `Permission`
  to the Actor surface block (Find/Add/Revoke). The Actor block was
  the only spot in the file that was stale; rest of the tree is
  current.
- **Wrote `Documentation/v0.2/filesystem-permission.md`** (~140 lines):
  a new architecture doc for the permission system. Covers Authorize,
  the two grant homes, Ed25519 8-step verify with SkipFreshnessCheck
  scoped to steps 2 and 4, revoke, and the known follow-ups.

No character-proposals.md on this branch — nothing to evaluate.

## Verdict: PASS — ready to merge

Code is reviewer-green and docs are current. The five files touched in
this pass (4 modified, 1 created) are listed in `v1/result.md` and
`docs-report.json`.

## Code example — the F1 doc fix

Before (lying since the F-A merge):

```csharp
///   - <b>Session ("y")</b> — no expiry on signature, lives in an in-memory
///     list, dies when the App exits.
///   - <b>Persisted ("a")</b> — signature has an expiry, routed to
///     <c>app.SettingsStore</c> under the <c>permission</c> table.
```

After (matches implementation + sibling class doc):

```csharp
///   - <b>Session ("y")</b> — unsigned, lives in an in-memory list, dies
///     when the App exits.
///   - <b>Persisted ("a")</b> — Ed25519-signed with <c>Expires == null</c>
///     (permanent), routed to <c>app.SettingsStore</c> under the
///     <c>permission</c> table. Verified with <c>SkipFreshnessCheck=true</c>
///     so the wire-freshness window doesn't apply; the signature's own
///     <c>Expires</c> field is the only time bound.
```

The pattern (a fix that updates one of two sibling classes describing
the same concept) is captured in the codeanalyzer v5 learnings.

## What's next

```
VERDICT: PASS — ready to merge.
```
