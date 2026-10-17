# auditor — kind-is-type

**Version:** v1

**What this is:** second-opinion review of the kind-is-type merge (branch of runtime2 at `cf66e2d01`, tip `49c09aaf6`). Three reviewers had already landed PASS on their scopes (codeanalyzer v2/v3, tester v2, security v2). The auditor's job per the character: review the reviewers, cross-file seams, architectural fit — not re-do the file-level passes.

**What was done:** PASS. Two non-blocking findings:

- **F-SC-1** (minor, deferred): the plan Why names "kind/strict ×4" as among the 16 reds this branch fixes; all four stay red at merge. Same on runtime2 (not a regression) — but the plan's own named proof is unfulfilled. Architect to rescope `start.goal:48` and the Why, or carry explicitly in the summary like F3.v3.
- **F-FL-1** (nit, informational): confirms the codeanalyzer's F3.v3 (`Fluid.cs:165 format is { Name: "item" }`) is the only new "is X? by name" check on the branch — the branch removed one of the same family (F4.2) and added this one. Not a cluster; the carry to branch A is complete.

**Seams verified by direct read:**
- `data.Open()` is really one parse-once door (six callers + `Value<T>`, no residue of the gone navigator).
- `OwnInternal` chain terminates on `Family._internal` (the stored field), loop-free, matching 777's `Own*` pattern.
- `KindFollowUpTests` deterministically pins both Refine (M8) and the `!k.Internal` filter (M9/N1); the two false greens tester v1 found are closed.
- Locked-core additions (OwnInternal, file.read.Type, F4.2 face) each have an approving decision (831, 832, 834); re-pinned under the surface golden per tester v1's N3.
- `AsTextLandsTextUncaptured` is in the plan's `start.goal` index (closed tester v2's note).

**Files modified:** `.bot/kind-is-type/auditor/v1/{report.md, verdict.json}`, `.bot/kind-is-type/auditor/summary.md`.

**Code example** (the one-door seam):

```csharp
// data/this.cs — Open() is the parse-once door; every use passes through it
internal async ValueTask<app.type.item.@this> Open() {
    var value = await Value();
    if (!Success || _context is null || value is not source rest) return value;
    try {
        if (rest.Parse(_context) is not { } parsed) return value;
        parsed.history.Add(rest);          // the source rides as history
        if (rest.Cacheable) _item = parsed; // narrow (787)
        return parsed;
    }
    catch (JsonException or FormatException or InvalidOperationException or NotSupportedException ex) {
        Fail(rest.Unread(ex, this));
        return value;
    }
}
```

**Next bot:** docs — write up stages 0–6 and the three locked-core adds; carry F-SC-1 if the architect doesn't rescope; F3.v3 goes to branch A alongside the #3 floor and F8.1.
