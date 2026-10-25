# json-lines — final audit (auditor)

**Verdict: FAIL, narrow.** Tip 5b15532c3 (parent runtime2 0a178687b). Next: coder, three cheap items (C1–C3). Everything
else holds. If the architect rules C1–C3 to the next branch, the verdict is PASS and nothing else blocks the merge.

Checked at the tip (OPENAI_API_KEY unset for every run, so no call could leave): `./dev.sh build` clean (74 s). Sweep:
Modules 1,419, Types 1,000, Data 879, Generator 191, Runtime 1,390 green; Wire 612 with 6 red, the baseline's names.
`plang --test`, **llm included, no key**: 308 total, 303 pass, 1 fail (BareLiteralJudgedByForm, the parent's), 3 stale,
1 skipped; the tree stays clean after the run. So the six llm tests are served from their re-saved files, and a pass
rewrites none of them (the layout is reproducible). The live llm gate (OpenAI accepts the requests) I did not run; it needs
the key and the tester ran it (6/6 live, no file changed).

## Reviews assessed
- codeanalyzer (FAIL v1, PASS v2): agrees. F1 (cursor item ignored by the Windows window) was a real seam; its fix, back to a
  text line through `cursor.Line`, holds (`cursor/this.cs:26-36`, `headless/this.cs:202-206`). **Gap:** review.md:21 says list's and
  item's files were "read at the methods; older smells not read". They are touched files; see C1.
- tester (PASS): agrees. Its three fixes are in (Line, the tool-result test, plan names).
- security (PASS, per architect 18; its only artifact is the probe `KindWalkWriteProbeTests`, no report folder): agrees; I
  read `kind/this.cs:62-74`: only the layout writes, onto the context's settings.

## Findings

| id | sev | file:line | issue | missed_by | disposition |
|---|---|---|---|---|---|
| C1 | minor (boy scout) | `type/item/list/this.cs:81,96,127`; `type/item/this.cs:118` | The branch touches these two files (one member each, `4a2dbc805`) and left their older smells. Creative names: `each` (:81), `made` (:96), `born` (:127), `rec` (item :118), `held` (item :821). A name where a rename does it. | codeanalyzer (did not read them whole) | pre-merge: rename |
| C2 | minor (error rule) | `window/this.cs:65,67` | The branch rewrote these two lines. A failed encode is `continue`d and `file.Append`'s answer is dropped, so an error record is lost with no sign. A background loop has no caller to answer, so it writes to the debug channel (`context.App.Debug.Write`). | codeanalyzer noted the `continue` as older and left it | pre-merge: debug line on both |
| C3 | nit | `window/this.cs:88,94` | `Said` still hand-builds `stats.jsonl`/`media.jsonl` lines by splicing guest text, beside `Kept` which now uses the file's kind. Different shape (guest json text, not a value), so not a one-door violation. | none | next branch (X3) |

C1 is the only real work: 5 renames. C2 is two debug lines.

## Decisions against the code

| # | says | found |
|---|---|---|
| 1,3 | jsonl kind; a list's elements are its lines | `kind/jsonl/this.cs:10-38` (`.jsonl`, `.ndjson`, `application/jsonl`, `x-ndjson`, LF line end, `Open` never throws); `list/this.cs:319-324` each element a `Record`; `item/this.cs:916-919` default one record. Parse names the line (`jsonl/this.cs:53-58`). |
| 4 | one-line rule in the jsonl writer | `writer.cs:57-74`: a LF or CR in a compact record can only be a paste; it is laid again through `JsonDocument`. Tokens refuse (:88-107). `DisposeAsync` frees both writers (:77-82), `Encode` awaits it. |
| 2 (1),7 | errors.jsonl written by the file's kind; no hand `"\n"` | `window/this.cs:51,65-67`; the LF comes from the writer. |
| 8,10 | json formatted by default, `layout`, LF, every character as itself | `json/this.cs:42-58`: `Indented`, `NewLine = "\n"`, `UnsafeRelaxedJsonEscaping`; `json/setting/this.cs:15` default `formatted`; `layout.cs` the choice. Wire, digest and .pr have writers of their own and never read it (`JsonLayoutSettingTests`). |
| 11 | kinds walkable | `node/root/this.cs:15-21` (`kind` = item's kinds); `kind/this.cs:62-74` (own members, then a kind by name unless `setting`, else the type); `:72-73` a kind's `Setting` is its own class's. |
| 13,14,17 | the cursor is a text line | `cursor/this.cs:26-36` writes it through the text kind and answers its Data; `headless/this.cs:202-206` ignores a failed one (the pointer stays); `Window.Field` finds `cursor` (HeadlessCursorTests). |
| 2 (survey) | llm tool result goes through the text kind | `OpenAi.cs:534`. Saved compares both sides under the same layout (`Saved.cs:114-122`), so a file is served whichever way it was written. |

## Cross-file check of json Encode's callers (the default changed for all)
I listed every `.Encode(` caller. Store: plang wire (`store/sqlite/this.cs:43`), not json. Egress: plang wire. `parent/this.cs:96`
and `display/this.cs:293` encode, then `JsonNode.Parse` the text, so layout is invisible. Fluid's `| json` re-escapes `<`,
`>`, `&` itself (`Fluid.cs:146-147`) and the builder templates pipe scalars only. `base64` and an http body are formatted, as ruled.
Nothing signs or hashes json Encode's bytes.

## Locked surface
`git diff 0a178687b..HEAD -- surface_golden.txt` is +55 lines, each in a commit naming its decision: the jsonl kind and writer, the
`Write(jsonl.Writer)` virtual and list's override (json-lines 3); `json.setting` and `layout` (bugs 23, json-lines 10);
`kind.Setting` and root `Get` overrides (json-lines 11). Nothing else moved.

## Rule 35
`cursor.Line` is internal and production's headless calls it; `Writer.Record` is internal and used by item and list. The one
test-side member is `PLang.Tests/Shared/JsonLayoutExtensions.Compact` (a test file). No production member exists for a test.

## Next-branch list (older smells in the two big files, read by agent and confirmed at C1's lines)
- X1 `list/this.cs`: the `_index`/`_rows`/`_chunks` copies of `_items` each with its own drop rule (split lifecycle, :63-236); the `int` doors beside the
  number ones (`At`, `Stored`, `Locate`, `Insert`/`RemoveAt`/`SetAt`); `Lower` type-switch (:716); `OrderOf`/`SortAsync` private statics (:512,534);
  `CountRaw`, `AddRaw`, `ResetTo`; `Type.Template == null` tested at three sites.
- X2 `item/this.cs`: the two `Holds` overloads (:400,:414) share one body (fork); `Pour`/`Debug` build `kind.json.Writer` in the base (:806,:831).
- X3 `window/this.cs` `Said` (C3).
- X4 docs: `Documentation/` still says json is compact/HTML-escaped where it does (grep `application/json` in data-spec, good_to_know); the
  `docs` bot should check the json-setting page.
- X5 the Windows window's real Draw of the cursor line (Chromium cannot run in a test).
