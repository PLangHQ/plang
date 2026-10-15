# security — kind-is-type

**Version:** v1 (first review).

## What this is

The architect's partial-scope review of kind-is-type: four pieces that landed on the branch and touch security — stage 0 (the .pr's goal path), 767 (goal name `..` block), 789/790 (settings read-from-record), and the decider-key resave. The rest of the branch is still being coded; the architect said to leave it for his go.

## What was done

Read-only review against parent `runtime2@cf66e2d01`. Semgrep clean (24 baseline hits, no new). Static trace of each piece against the parent-branch baseline and the standing patterns in memory (AuthGate canonicalisation, [Sensitive] discipline, signing pipeline, settings isolation).

**Outcome: FAIL — one High.** Stage 0's rename closed the old `"path"` smuggle door, but the new `"file"` door is unvalidated: `goal/serializer/Reader.cs:96` composes `pr.Parent.Parent.Combine(file).Relative(ctx.Context)` with `file` read verbatim at line 69. `Combine` is `System.IO.Path.Combine` — a rooted `file` returns `file` whole; a `../..` climbs and gets resolved by the FilePath ctor's `Canonicalize`. The resulting `goal.Path.Folder` can be made to point under `app.OsAbsolutePath`; `path.Authorize.AskedByOs` then auto-grants that goal's steps silently — the exact threat decision 745 named. Reach is the same as 745: a plang app shipped with its `.build/*.pr` pre-built that the user runs without auditing.

Fix direction (one line in Reader.cs): refuse `file` with any path separator / `..` segment / rooted form, keyed `PrFormatOutdated` — mirrors the architect's own words that `"file"` is only a goal file's name. Alternative (silent): `Combine(pr.Parent.Parent, Path.GetFileName(file))` — mirrors line 71's old-shape reduction. Prefer the explicit refusal.

**Verified clean:** 767's `..` block is sound and covers every goal-name-to-path door (`Find` is the single door). 789/790 settings read-from-record keeps class discipline (Record.cs:37 uses the owner's own ClrType; setting.Create guards `IsAbstract:false`, `!= base`, `IsAssignableFrom` and refuses unknown options); permissions still ride the signed wire so no row can be misread as granting more. No secret landed on the branch: the one `.sqlite` fixture that was added and later removed holds an empty `key` field.

Files touched (output):

- `.bot/kind-is-type/security/v1/plan.md`
- `.bot/kind-is-type/security/v1/result.md`
- `.bot/kind-is-type/security/v1/verdict.json`
- `.bot/kind-is-type/security-report.json`
- `.bot/kind-is-type/report.json` (appended my session)

Next bot: **coder** (F1 is a one-site change at `goal/serializer/Reader.cs:69`). Once coder lands, auditor last.

## Code example — the hole and the shape of the fix

Today (`PLang/app/goal/serializer/Reader.cs`):

```csharp
case "file": file = reader.String(); break;           // :69 — accepted verbatim
...
if (parent == null && file != null && ctx.Origin is { } pr)
    goal.Path = pr.Parent.Parent.Combine(file).Relative(ctx.Context);  // :96
```

Shape of the fix (refuse, mirror :88's PrFormatOutdated door):

```csharp
case "file":
    file = reader.String();
    if (file.Contains('/') || file.Contains('\\') || file.Split('/','\\').Contains("..")
        || PathHelper.IsPathRooted(file))
        throw new global::app.error.PrFormatOutdatedException(
            $"'file' must be a goal's file name, not a path: '{file}'", ctx.Origin);
    break;
```
