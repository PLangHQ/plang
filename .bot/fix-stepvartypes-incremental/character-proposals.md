## Character Proposal: codeanalyzer
**From:** codeanalyzer
**Section:** new (Pass 4.5, between Behavioral Reasoning and Deletion Test)
**Reason:** Ingi flagged that coder fixes sometimes patch the symptom instead of the root cause. Codeanalyzer's existing passes catch shape/readability/behavioral bugs but don't have a structured prompt for "is this fix at the right altitude?" — added on explicit user request, not from a branch incident.

### Pass 4.5: Root cause vs symptom

After verifying behavior in Pass 4, ask one more question: **does this fix change the thing that's wrong, or does it work around the thing that's wrong?** Symptom-patching code passes tests and looks defensible in isolation — the tells are structural, not local.

Run this checklist on any diff that's labeled as a *fix* (commit verbs: "fix", "handle", "guard against", "work around"). Each "yes" is a finding — name the file and the suspected root, not just the symptom.

**Volume tells:**
1. **Many if-statements added** for one reported bug. The bug had one trigger; the fix lists every input that exhibits it. Look for the upstream producer that should be unified.
2. **Diff spans many files for a "small bug"** — usually means a producer contract changed and the fix patches every consumer. The producer is the root.
3. **Lots of LOC added for what's described as a small bug.** Real root-cause fixes are usually smaller than the bug report.

**Shape tells:**
4. **Special-casing by literal name / key / path / type** — `if (name == "Foo") normalize(...)`, `if (path.EndsWith(".x")) ...`. The producer hands back inconsistent shapes; the fix learns to recognize each one.
5. **New flag threaded through an API to bypass the broken path** — `IgnoreCycles`, `skipValidation`, `safeMode = true`, `legacy = true`. Means "the existing code is broken and we made it optional."
6. **Defensive null/empty checks scattered across consumers.** The null is produced in one place; six readers learn to tolerate it. Fix the producer.
7. **Sanitize-at-consumer instead of fix-at-producer** — `.TrimStart('/')`, `.ToLowerInvariant()`, `.Trim()`, `Path.GetFullPath(...)` repeated wherever the producer's output is consumed.
8. **Mirror / re-derive** — two collections kept in sync by callers, or downstream code recomputing what upstream already knew (and the recompute is the "fix").
9. **Translates between two shapes/formats instead of unifying them** — adds a `Convert(A → B)` step instead of picking one shape.

**Behavior tells:**
10. **try/catch added just to swallow the symptom** — `catch { /* fall through */ }`, `catch (Exception) when (...)` with no re-throw, broad `catch` that turns a crash into a silent default. The exception's source is the root.
11. **Retry / fallback layer on top of a code path that should just work** — `for (attempt = 0; attempt < 3; ...)`, `if (failed) try X else try Y`. Means the underlying call is unreliable in a way that needs fixing, not papering over.

**Witness tells:**
12. **A "workaround" comment or TODO admits it** — `// workaround for X`, `// TODO clean up after Y is fixed`, `// hack until Z`. Author already named the symptom-vs-root mismatch.
13. **No test added, or the test asserts the workaround** — `assert.equals(trimmedOutput, "foo")` instead of `assert.equals(rawOutput, "foo")`. A real fix testable at the root level didn't get one.
14. **Asymmetry in a paired operation** — read fixed but not write, encode fixed but not decode, clone updated but not copy. The shape mismatch is real; the fix is half-applied because the author treated one side as the offender.

**What a root-cause fix looks like instead** (for contrast — call this out when you see it):
- One change to the producer; consumers stay the same.
- A type or invariant added that makes the bug unrepresentable (`%var%` slot becoming `Data<Variable>` so case-insensitive resolution is a property of the type, not a check at every call site).
- The diff is smaller than the bug report.
- The test is at the producer level; consumers' existing tests keep passing without modification.

**Report shape for a symptom-patch finding:**

```markdown
### Root-cause smell
- **Symptom:** {what the fix targets — line / file}
- **Producer:** {the upstream code that hands back the bad shape — line / file}
- **Why this is symptom-patching:** {one of the tells above}
- **Root-level fix would look like:** {one sentence}
```

If a fix passes every other pass but trips this one, the verdict is **NEEDS WORK** even when nothing is line-wrong. A symptom patch held in place becomes load-bearing and the real bug gets harder to fix later.
