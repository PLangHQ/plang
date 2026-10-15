# security v1 — F1 re-check on d9bc6aae7

## Verdict: PASS

The fix at `goal/serializer/Reader.cs:69-80` closes F1. The `"file"` field is validated **before** it is combined, and the refusal is loud (`PrFormatOutdatedException` — the same door the unknown-key refusal at line 88 uses).

Predicate:

```csharp
if (named0.Length == 0 || named0 is "." or ".." || named0.IndexOfAny(['/', '\\', ':']) >= 0)
    throw new PrFormatOutdatedException(
        $"the .pr's 'file' must be a bare goal-file name, not '{named0}' — rebuild it", ctx.Origin);
```

Covers the F1 vector: a rooted form (`"/x"`), a drive form (`"C:x"`), a `..` or `.` segment anywhere, a parent-reference string on its own. Loud refusal with the hint "rebuild it" — matches the architect's wording.

## Edge-case probe (what the architect asked me to check)

- **`C:x` and `C:\x`** — refused. The `:` catches drive-letter forms; `\` catches rooted Windows forms. ✓
- **UNC `\\server\share\x`** — refused. Leading `\` caught. ✓
- **NT device path `\\?\C:\x`** — refused. `\` and `:` both caught. ✓
- **NTFS alternate data stream `foo:bar`** — refused. `:` catches it. ✓ (Correct — on Windows, `fopen("foo:bar")` opens the `bar` stream of `foo`, bypassing filename hygiene; a `.pr` has no business naming one.)
- **Trailing dot `"foo."` / trailing space `"foo "`** — passes the validator. On Windows these alias to `"foo"` at the filesystem layer, but Combine still produces a path **inside** `pr.Parent.Parent` — no climb, so no AskedByOs reach. **Not a finding.** Noted as a minor cosmetic quirk; if someone wants it tighter it is a `.Trim().TrimEnd('.')` on `named0`, but the trust surface is unaffected.
- **Null byte `"foo\0bar"`** — passes the validator; .NET's Path/Combine throws on nul before the Canonicalize completes; load fails. Fail-safe.
- **Unicode separator lookalikes (U+FF0F `／`, U+2044 `⁄`, U+2215 `∕`)** — pass the validator (char-level `IndexOfAny` on `/\:` doesn't match them). .NET path APIs treat them as literal characters, not separators, so they become part of the filename inside `pr.Parent.Parent`. No climb, no escape. Safe.
- **Dot-prefix literal `..foo`** — passes (not literal `..`). Literal filename starting with "..". No parent-reference semantics in any filesystem. Safe.
- **URL-escaped `%2e%2e`** — passes; literal filename. The filesystem doesn't decode it. Safe.
- **Windows reserved names `CON`, `NUL`, `COM1`** — pass the validator. On Windows, accessing them opens a device, not a file; the subsequent `.pr.Read` for the goal file fails (nothing there) and load errors. Fail-safe — no trust elevation.
- **Empty string `""`** — refused by `Length == 0`. ✓
- **JSON `null` value** — the json reader returns an empty string / throws; both land in fail-safe territory (empty caught; throw propagates as `MaterializeFailed`). ✓
- **A `.pr` with NO `"file"` key** — `file` stays null, Reader.cs:96 skips the Path assignment, `goal.list.Add` throws on `PrPath == null` (list.this.cs:40-41). Load fails. Fail-safe.

## Other readers that take a `.pr`'s location fact?

Checked `ctx.Origin` usage across the tree:

- Composition sites (where Origin becomes a Path): **one and only one** — `goal/serializer/Reader.cs:96`. The one gatekeeper is now validated.
- Everywhere else `ctx.Origin` appears (step/serializer/Reader.cs:49, variable/serializer/Entry.cs multiple, data/reader/this.cs:102, goal/serializer/Reader.cs:88,92) — Origin is passed as the error-reporting field on `PrFormatOutdatedException`, not used to construct any path.

The old-shape `"path"` branch (Reader.cs:71) stays safe-by-reduction: `path.@this.Resolve(reader.String(), ctx.Context).Name.ToString()` strips to the final segment, so a hostile old-shape `"path": "/system/evil.goal"` yields `.Name = "evil.goal"` — then combined into `pr.Parent.Parent`, inside the folder. Both doors land `file` as a bare name before the combine; the two converge.

## Pins

`PLang.Tests/Runtime/App/Goals/GoalPathTests.cs:118-150` — `APrFile_ThatNamesAPath_IsRefused` with `[Arguments("../evil"), ("/evil"), ("sub/evil"), ("..\\evil")]`. Each builds a real `.pr` with the hostile `"file"` and asserts the load is refused. Plus the existing seven GoalPathTests cases (moved goal loads, old-path/system-trust, system-form rendering, relative-file-in-folder). 11/11.

## Note (not a finding)

The predicate uses `named0 is "." or ".."` for the exact-match case. That's narrower than `Split('/','\\').Contains("..")` (which 767 uses for name climbing in `goal.list.Find`). Both work here because `IndexOfAny(['/','\\',':'])` has already caught any multi-segment path — any `..` sitting mid-string must be in a path, which is rejected. So the exact-match-only shape is sound in this one-token context, and it would be wrong to copy 767's segmentation form here (it would miss `.` as a lone segment).

## Where this ran

Worktree `/workspace/plang-kind-is-type-security` on `origin/kind-is-type` after fetch (`48c0fc31b`). Static review only; the pins' pass claim rests on the architect's "11/11" statement — I did not rebuild to re-run them because the predicate is simple enough to verify by inspection.
