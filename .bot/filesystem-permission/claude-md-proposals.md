# CLAUDE.md proposals — filesystem-permission

## architect — v1 — 2026-05-19
**Target:** `Documentation/v0.2/good_to_know.md` — under the existing "OBP Smell Checklist" section (a new smell, or strengthening the smell about owners-of-data).
**Why:** During the filesystem-permission design pass, the architect repeatedly wrote helper-soup code (`ResolveAbsolute(path)`, `CheckOrRequest(absolute, verb)`) — helpers that take a domain object's data and return a derived answer. Ingi called this out as an OBP violation: the domain object should own its own questions. `path.Absolute` and `path.HasPermission(Verb.Read)` are the right shape, not `Resolve(path)` and `Check(absolute, verb)`. The existing smell list catches "public mutable collection with rules enforced from outside" but doesn't name this specific helper-soup pattern that's tempting to write when sketching designs in C#-shaped pseudo-code. Worth a sharp explicit rule.
**Proposed change:**

Add a new smell to the OBP Smell Checklist (after smell #4, as #5), and add a worked example in the "Worked example" appendix.

Add to the numbered smell list:

```markdown
5. **Helper that takes a domain object and returns a derived answer.** A free function (private, static, or external) takes `Thing` and returns some piece of its logic — `ComputeAbsolute(path)`, `CheckPermission(absolute, verb)`, `RenderName(user)`. The domain object owns its own questions; if you find yourself writing `Helper.X(thing)`, ask whether it should be `thing.X()`. Almost always yes. The helper is the missing method on the type.
```

And a worked example block (style matching the others in good_to_know.md):

```markdown
### Worked example — Helper-soup vs. self-owning methods

Smelly:

    public async Task<Data<string>> ReadText(Path path) {
        var absolute = ResolveAbsolute(path);
        var check = CheckOrRequest(absolute, Verb.Read);
        if (check is { } request) return request;
        return Data.Ok(await File.ReadAllTextAsync(absolute));
    }

`ResolveAbsolute` and `CheckOrRequest` are helpers taking Path's data and producing answers. The method body is wiring outputs from one helper into the next — a transaction script dressed as OBP.

Self-owning:

    public async Task<Data<string>> ReadText(Path path) {
        var check = path.CheckPermission(Verb.Read);
        if (!check.Success) return check;
        return Data.Ok(await File.ReadAllTextAsync(path.Absolute));
    }

`Path.Absolute` and `Path.CheckPermission(Verb)` are methods on Path — it owns those questions. The FS method only does what only it can do: the actual IO via the BCL. Two delegations, no helper wiring.

Litmus test: count private static helpers in the calling class. Each one is suspicious — it's a method that didn't make it onto the right type.
```

## tester — v1 — 2026-05-21
**Target:** `CLAUDE.md` — new subsection under "## Running plang Tests" (or a standalone "## Mutation Testing" section).
**Why:** During the v4 tester review, the tester edited production source three times (`Path.cs` `RootComparison`, `Path.Operations.cs` `isMove`, `Actor/Permission/this.cs` `Find`) as mutation tests — deliberately breaking behavior to confirm a test dies, then reverting. This is a core, legitimate tester technique and the edits were reverted immediately (`git status` clean, nothing committed). But Ingi saw the edits land mid-review and asked, alarmed, "why are you changing code?". The technique is sound; the problem is it is *silent* — a source edit to a security-relevant file looks identical to an accidental or unauthorized change until you read the surrounding context. A one-line announcement before the first mutation removes the alarm with zero cost. Filed at Ingi's explicit request.
**Proposed change:**

Add a new subsection to `CLAUDE.md`:

```markdown
## Mutation Testing (announce first)

Before editing production source to run a mutation/deletion test — deliberately
breaking behavior to confirm a test catches it — say so in plain text first:

> **Mutation test:** about to temporarily edit `<file>` (`<what changes>`) to
> verify `<which test/finding>`. Will revert immediately; nothing committed.

This is a legitimate and expected technique (testers, reviewers). The
announcement exists only so a watching human never has to wonder whether a
source edit to a security-relevant file is intentional. Rules:

- Announce **once** before a batch of mutations, not per file.
- Always revert before moving on; end with `git status` clean.
- Never commit a mutation — source stays untouched in the final diff.
```

---
**Footnote (reviewer-bot exception):** Per CLAUDE.md, reviewer bots (tester) do
not propose CLAUDE.md changes on their own. This proposal is filed under the
stated exception — explicit user request after a real incident on the branch
(Ingi asked for it directly after being surprised by a mid-review source edit).
