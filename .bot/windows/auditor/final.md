# windows — final audit (auditor)

**Verdict: PASS.** Tip 10c557561 (parent runtime2 b4a549d1c). Next: docs (W2 first). Nothing blocks the merge; one call for the
architect on the boy scout rule (W5).

Checked at the tip, OPENAI_API_KEY unset: `./dev.sh build` clean. Sweep: Modules 1,434, Types 1,008, Data 880, Generator 191,
Runtime 1,409 green; Wire 612 with 6 red, the baseline's names. `plang --test` (llm included, saved answers): 309 total, 304 pass,
1 fail (BareLiteralJudgedByForm), 3 stale, 1 skipped; tree clean. **Windows:** I cannot run it. The host's logs say: the full run at
418109e8d (`failures-3.md`: 88 of 100 green, the 7 unexpected traced), then reruns of the classes the later commits touched
(`rerun-4/5/6.log`: OriginFact, GoalCallResolution, FileUrl, GoalPrPath, Birth, PlangForm, GrantMatch 14 + 1 skipped, RequiredVariable,
OnEvent). No full Windows sweep exists at the tip; the commits after 418109e8d that touch Windows behaviour (the glob matcher, the
`query` render, the guard) are covered by those class reruns only.

## Reviews assessed
codeanalyzer (FAIL v1 on a comment, PASS v2): agrees on the branch's lines; its scope was "read whole at the touched members" (W5).
tester (FAIL v1 on a false-green plang test, PASS v2): agrees; `EachGoalIsTheAppsOwn` now goes red with the Level lines reverted.
security (PASS; the differential probe): agrees. I read `permission/this.cs:117-159`: one forward pass per piece over `bool[n+1]`,
no backtracking; `*` stops at either OS separator, `**` crosses, `?` is one character of a part; literals compare with the exact
grant's own `Comparison`; `Comparison` fails closed (`ParseScheme` needs `://`).

## Decisions against the code
- Each owner decides its own Level: `goal/this.cs:392-397`, `step/this.cs:118-123`, `action/this.cs:156-158`; no check in on.event.
- Debug by address: `debug/setting/this.cs:24-37`, `debug/this.cs:153-186`; documented in `debug.md`.
- Typed path form: `file/this.Typed.cs` (one private owner of `/` math), `Derivation.cs:22,35,43`; `Resolve` compares both canonical forms (`file/this.cs:121-125`).
  I checked the one behaviour change, `Combine("/x")` now under the path: every caller passes a name (`Parent.Combine(".build")`, `goal.Stem + ".llm.json"`, `Reader.cs:107`).
- `[IsNotNull]` refuses a variable holding nothing: generator `Emission/Action/this.cs:401-418`, lookups only (`Hop.IsLookup`, `Method` false). Reads the variable by its own door; the slot is untouched.
- llm.query renders its messages at use: `query.cs:62-83`, `OpenAi.cs:70-74`.
- Store folder 0700: `sqlite/this.cs:109-114`. LF: `.gitattributes`, `goal/this.cs:78`, `this.Item.cs:34`, `process/this.cs:232`.
- Locked surface: no golden diff. Rule 35: `Hop.IsLookup`, `Address`, `Typed` are all used by production.

## Findings
| id | sev | where | issue | disposition |
|---|---|---|---|---|
| W1 | minor (error rule), not reproduced | `sqlite/this.cs:112-114`, `file/filesystem/this.cs:126` | Open now chmods its folder with `File.SetUnixFileMode`, which throws a raw `UnauthorizedAccessException` when the folder isn't the user's (a group-writable folder made by another user). Mkdir's failure just above is answered as an `AppException`; this one isn't. A plain .NET exception here means plang looks broken, not the folder. Wrap it in the `Mode` verb. | next branch |
| W2 | minor (docs) | `Documentation/v0.2/debug.md:41` | says debug attaches at the goal "and what it calls"; for a system goal named by address, the system goals it calls stay unshown (ruling 13, Ingi agreed). | docs |
| W3 | nit | `debug/setting/this.cs:32-34` | the address is compared against `Path.Raw` with `.goal` cut by hand, where `goal.Address` exists; the two can spell a path differently. Pinned for `/system/...` by `DebugAttachTests`. | next branch |
| W4 | nit | `process/this.cs:232` | a child's stdin line now ends at LF for every program, native Windows children included. Ruled (stage 6), proven for `wsl.exe`; not for a native console child. | note |
| W5 | for the architect | the 20 touched production files | The codeanalyzer read them "at the touched members". Two agents read the 13 largest whole and found older smells (not this branch's): `goal/this.cs:184-205` (`FolderPath`/`Folder`, `FullPath`/`Address` stored twice), `:597-604` creative names (`currentGoal`, `stepNode`, `pendingComment`); `step/action/this.cs:116-132` (module/action string compares beside the `clause` subclass), `:23,73` late stamp; `process/this.cs` (`_said`/`Heard`, `Lines` twice, `Write(string)`); `file/this.cs:137-154` (`Replace('\\','/')` at 3 sites + `Typed`), `Canonicalize`/`Trimmed` statics; `permission/this.cs:55,74` string `Path`. The branch's own lines are clean of named smells apart from W3 and `Step.IsSystem` (a middleman, `step/this.cs:125`). Held to json-lines' standard (C1), the creative-name renames here would be pre-merge too; I have not held this branch for them, since the files are touched narrowly and the rest is structural. Your call; if you want them, it is a rename list, no logic. | architect |

## Next-branch list
X1 W1 and W3. X2 the older structural smells in W5 (stored-twice in goal, the action's string forks, process). X3 the full Windows
sweep at the final tip (the host), and `text-culture` (Ingi). X4 `BareLiteralJudgedByForm` has no owner (architect 41).

## Update — W1, W3, W5 closed at 1506c9c75 (PASS)
W1 (`a48405461`): `Mode` and `Mkdir` answer `IOError` 500 for an IO or access refusal (`this.Operations.cs:281-289, 364-372`); the store carries it (`sqlite/this.cs:107,113`); `AFolderAnotherUserOwns_AnswersItsModeRefusal` (skips as root). W3 (`ac3723faa`): debug compares `goal.Address`. W5 (`1506c9c75`): `goal.Parse` names; I read the diff: the step's indent `(raw.Length - trimmed.Length)/4` equals the old leading-space count (`trimmed` is `raw.TrimStart()`), the comment is captured before it is cleared, and steps add through `goal.Step`; no logic moved.
Sweep: Wire's 6 baseline reds only (Modules 1,434, Types 1,008, Data 880, Generator 191, Runtime 1,410 green). `plang --test`, llm saved: 309 total, 304 pass, the baseline's 1 fail, 3 stale, 1 skipped; tree clean.
New finding **W6 (nit, next branch):** `screen/type/screen/display/this.cs:47` calls `folder.Mkdir(context)` and drops the answer; before W1 a refusal threw, now it passes silently and the socket steps after it fail with their own error. Return the answer as line 52 does.
