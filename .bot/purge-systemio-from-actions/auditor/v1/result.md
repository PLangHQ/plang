# auditor v1 — result

**Verdict: FAIL** — one MAJOR review-gap finding.

The branch's stated goal (purge System.IO from action handlers, route every
disk touch through `path.@this` → `AuthGate`) is *technically* met. PLNG002
at error severity enforces it at compile time. codeanalyzer, tester, and
security all signed off.

**But the PLang `--test` suite is not green on a clean rebuild.** Two tests
regress, both introduced by the F1 security fix (`064724fda`). The tester
v2 report claims `PLang 206/206 pass` — that claim is wrong.

## Assessment of the prior reviews

| Bot | My read |
| --- | --- |
| codeanalyzer v1 | **agree** — the 5 LOWs are accurate; N1/N5 were closed in coder commit 012b1d74c; N3 footgun stands but is documented and currently uncalled in prod. |
| tester v2 | **disagree on test count** — the `206/206` figure does not reproduce. C# suite is real (3031/3031 on my rebuild). |
| security v2 | **agree on security** — F1 mutation-tested clean; F2/F3 closed; PLNG002 carve-outs (`IsPathHelperFile` / `IsPathTypeSurface`) are visible at the use site and not bypassable from elsewhere. |

## Verification I ran myself

Clean rebuild from the F1-introducing commit forward:

```
rm -rf PlangConsole/bin PlangConsole/obj PLang/bin PLang/obj \
       PLang.Tests/bin PLang.Tests/obj \
       PLang.Generators/bin PLang.Generators/obj
dotnet build PlangConsole          # 0 errors, 0 PLNG00x
dotnet run --project PLang.Tests   # 3031 / 3031 pass
cd Tests && ../PlangConsole/bin/Debug/net10.0/plang --test
                                   # 204 / 206 pass  ← regression
```

Same command on `runtime2` baseline: **206 / 206 pass**.

## F1 — MAJOR — PLang suite regresses 206→204 on full run

**Bisect.** `git checkout 064724fda^` (the commit just before the F1 fix) → 206/206.
`git checkout 064724fda` (F1 fix landing) → 204/206 with the same two failures
at the same call site. The F1 fix is the introducing commit.

**The failures.**

```
[Fail] Builder/CompileLlmNotes/output-write-no-channel.test.goal
    Error: Channel 'input' has no interactive answerer (stream EOF)
[Fail] Builder/CompileLlmNotes/assert-equals-no-message.test.goal
    Error: Channel 'input' has no interactive answerer (stream EOF)
```

Both tests' first step is:

```
- copy file '../../Simple/.build/start.pr' to 'start-out.txt', overwrite
```

"`input` has no interactive answerer (stream EOF)" is the signature of an
AuthGate prompt that fired against a non-interactive channel. So after F1's
canonicalization, this copy is no longer being silently auto-granted by
`IsInRoot` — it is escalating to a permission prompt, exhausting the test
runner's input channel.

**Why F1 changed the behaviour.** Pre-F1, `_absolutePath` was stored with `..`
segments intact; `IsInRoot` did a textual `StartsWith(appRoot)` and the
path `<root>/Tests/Builder/CompileLlmNotes/.build/../../Simple/...` textually
prefix-matched root, so the silent fast-path granted. Post-F1, the ctor
canonicalizes via `PathHelper.GetFullPath` first, so `IsInRoot` sees the
resolved absolute. For these two tests the resolved absolute apparently
falls outside the prompt-free zone — exactly the case F1 was designed to
catch. **F1 is right; the test setup or the runtime-dir anchoring is now
wrong relative to F1.** This is what F1 was *for* — and it's catching a
real case, but in legitimate user-test code, not a path-traversal attack.

**Order dependence (small surprise).** The two tests pass in isolation
(`--goal=...`) and fail in the full-suite run. Order-dependence on a
permission-grant cache is its own latent issue, but secondary to the main
finding.

**Why it's MAJOR not critical.** The PLang suite was the explicit
green-bar tester promised. Coder's handoff and tester's v2 round-2
report both name `206/206` as the merge gate. Shipping with that gate
red — and with the tester's number wrong — silently lowers the bar for
the next coder/tester pair to reproduce.

**What I recommend the coder do** (briefly, not a directive):
1. Reproduce on a clean rebuild and confirm the same 204/206.
2. Look at the runtime-dir anchor in `file.@this.Resolve` lines 88–106 for
   the test-runner case — what's `Goal.GetRuntimeDirectory()` for these
   two tests? If it's the goal's `.build/` (one level deeper than the
   goal dir itself), the test's two `..` segments don't reach `Tests/Simple/`
   without a third — F1 just made that visible.
3. Either fix the test's relative path (`../../../Simple/...`) and rebuild
   the .pr, OR adjust the runtime-dir anchor to match what the test
   author expected.

I am not asking for F1 to be reverted — it closes a HIGH-severity vuln
and is the right shape. The fix is on the test side.

## What I'm NOT flagging

- **N1 (Json.cs alloc footprint).** Addressed in `serializer/Json.cs:24–48`
  — converter alloc lives inside the `??` branch with a docstring naming
  codeanalyzer's finding. Clean.
- **N4/N5 (AppGoals indexing).** Addressed in `PLang/app/goals/this.cs:47–56,188–194`
  with inline comments naming the finding and the design choice. Clean.
- **N3 (implicit `string → path`).** Operator still present at
  `path/this.cs:204–205`. Audited prod call sites — every action-handler
  property is `Data<path>`, every Conversion site threads Context. The
  operator's documented contract holds; no in-tree producer is in the
  attack path. Standing.
- **PLNG002 carve-out logic.** Two-pronged: `Path.*` → PathHelper.cs only;
  `File/Directory/FileInfo/...` → `app/types/path/**` only. Verified by
  reading `Plng002.cs:151–163` against the actual diff. A hostile `File.*`
  added to PathHelper would still fire. Tight.
- **No PLNG002 suppression anywhere outside the analyzer itself.** Grepped
  the whole tree for `pragma warning disable PLNG`, `SuppressMessage(...,
  "PLNG002")`, `NoWarn` containing `PLNG`, and `.editorconfig`
  `dotnet_diagnostic.PLNG002` overrides — zero hits. `PlangTests.csproj`'s
  `<NoWarn>1701;1702;8602;8600;8625</NoWarn>` is generic CS only. The
  comment at `app/this.cs:82` explicitly says "no PLNG002 carve-out is
  needed here" — it routes through `PathHelper`, not a suppression. F2's
  `MarkdownTeaching.cs` whole-file exemption was deleted (coder
  987a5148e); F3's `app/this.cs` exemption was deleted (coder 064724fda).
  PLNG002 has **exactly two file-scope exemptions, both inside `Plng002.cs`
  itself, both visible at the use site, none smuggled through suppression
  attributes or csproj overrides** — answering Ingi's explicit check.
- **OsAbsolutePath fix (F3).** `app/this.cs:84–85` now uses
  `PathHelper.GetFullPath(PathHelper.Combine(...))` and the
  `#pragma warning disable` is gone. Whole-file exemption deleted.
- **The webui server.py (+1014 lines).** Not C#, not under `app/`, not in
  the security purge scope. Out of scope for this audit.
- **Sync `FilePath.Exists` / `Size`.** Still calls `System.IO.File.Exists` /
  `FileInfo` (`file/this.cs:58–69`). Security v2 explicitly flagged these
  as standing watch under the user-sovereign threat model — not in scope.

## Cross-file contracts verified clean

- **Goal.Path / Goal.PrPath flip (string → `path?`).** Consumer audit via
  `git diff runtime2..HEAD --stat` against the 18-file ring-2 sweep:
  every call site reads `.Path` / `.PrPath` as path-typed (Equals, dict
  keying, `.Absolute`, `.Combine`). No survivor of the old `string`
  shape was missed by codeanalyzer.
- **PathJsonConverter wiring.** Per-Actor `channels.serializers` bakes a
  Context-bound converter; `Conversion.TryConvertTo` builds a one-shot
  Context-bound options bag. No AsyncLocal, no ambient state. The implicit
  `string → path` lift is documented as the no-Context fallback (stub
  paths explode at Authorize — same contract codeanalyzer signed off on).
- **AppGoals dual-indexing.** `_goals` (PrPath-keyed), `_byPath` (Path-keyed),
  `_byName` — `Add/Remove/Clear` write/clear all three consistently;
  `Get` falls back to a by-form scan when name-lookup misses. The N4/N5
  comments in the source name the design and the rejected alternatives.
- **Execute verb (D8).** Three call sites — `code/load.cs:27–28`,
  `code/this.Snapshot.cs:97`, `module/add.cs:25` — all build `Verb { Execute }`,
  not `Verb { Read }`. `Verb.AllowAll()` covering Execute is test-only.

## Verdict

```
status: fail
summary: PLang --test regresses 206→204 on full-suite clean rebuild; tester's
         206/206 figure does not reproduce. F1 security fix is correct; one
         of the test-runtime-dir anchors needs to be aligned with it.
```
