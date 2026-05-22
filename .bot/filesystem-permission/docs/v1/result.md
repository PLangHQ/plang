# docs v1 result — filesystem-permission

## Changes applied

1. **`Documentation/v0.2/good_to_know.md`** — added OBP smell #5
   (helper-soup) and a "Helper-soup vs. self-owning methods" worked
   example. Renamed the existing "Worked example (this branch):" to
   "(collections — this branch)" to disambiguate the two examples now
   that the section has more than one. (architect proposal v1 applied
   verbatim.)

2. **`CLAUDE.md`** — added a new "## Mutation Testing (announce first)"
   section between "## Running plang Tests" and "## Debugging". The
   one-line announcement template matches the proposal verbatim.
   (tester proposal v1 applied verbatim, including the reviewer-bot
   exception filing context.)

3. **`PLang/app/actor/permission/this.cs:8-18`** — rewrote the
   class-header XML doc to match post-F-A reality:
   - Session "y" → unsigned, in-memory, dies with App.
   - Persisted "a" → Ed25519-signed with `Expires == null` (permanent),
     verified with `SkipFreshnessCheck=true`.
   (auditor v2 F1 closed.)

4. **`Documentation/v0.2/app-tree.md`** — added `Permission →
   app/actor/permission/` to the Actor surface block with a one-line
   summary of Find / Add / Revoke. Updated the Maintenance table row
   for "New `Actor` surface" with a Permission example. The Actor block
   was the only place app-tree.md missed; the rest of the file is
   unaffected.

5. **`Documentation/v0.2/filesystem-permission.md`** — new file. ~140
   lines. Covers Authorize flow, in-root short-circuit, Find two-pass
   lookup, the Ed25519 SkipFreshnessCheck rationale (with security v2's
   4-step bypass-scoping template), revoke, known follow-ups
   (RootComparison thread-through, Add dedup, bundled consent,
   output.ask shape), and a file map. Reader audience: PLang developer
   reading C# and debugging a permission denial.

## Decisions on proposals

| Proposal | Decision | Reason |
|---|---|---|
| architect v1 → `good_to_know.md` (OBP smell #5) | applied | Canonical rule for all future OBP work; concrete; pre-existing worked-example precedent. Applied verbatim. |
| tester v1 → `CLAUDE.md` (mutation announcement) | applied | Real branch incident, one-sentence rule, zero cost. Filed under explicit reviewer-bot exception. Applied verbatim. |

No character proposals on this branch — nothing to decide.

## Verification

- All five files read post-edit; intent matches implementation.
- The doc-comment fix in `actor/permission/this.cs` is XML-only — no
  compile risk. (No source code semantics changed.)
- The new architecture doc cross-references checked: `app-tree.md`,
  `good_to_know.md` OBP Variant Design section, `coder/v6/result.md`,
  `Documentation/v0.2/todos.md`. All targets exist.

## Verdict

**PASS — ready to merge.**

Branch is fully reviewer-green (codeanalyzer v5, tester v6, security v2,
auditor v2) and now docs-complete.

```
VERDICT: PASS — ready to merge.
```
