# codeanalyzer — snapshot-7c-wip, third check (narrow): 23d61537b → a690af41a/HEAD, 24 production files

Requested by the architect (153, 158). Every item of 153 and every production file touched since 23d61537b read (diff with its surrounding code; the three signature files and View/reader read whole). `kind.Read` is Ingi's yes (158), not flagged. No bench: the touched files on the birth and request path (`Diff.cs`, `call/this.cs`, `View.cs`) only lose code (Diff's two births are one, the dead frame `Rest` is gone), no store/pool change.

## 153's items

| item | status |
|---|---|
| 1 signature born with its view | FIXED. `signature.View` is an `app.View` taken at construction (`type/item/signature/this.cs:443`), `Hashed` and the `Origin`-fallback are gone, `Ed25519` hands the signing view whole (`Ed25519.cs:62`), verify re-hashes in `signature.View` (`:112`), the reader refuses a signature naming no view or an unknown name (`data/schema/signature.cs:96–99, 135–140`), `stored` compares by `view == View.Snapshot`. See S1 for the name written ×3. |
| N1 View.Reader / Make | FIXED. One private `View.Registered` (view's then the type's own), used by both; `reader.Registered` is the type's half. |
| N2 binding `Rest` | FIXED (deleted). |
| N3 Ed25519 throw | FIXED. `Verify` returns false for a key that isn't valid or isn't its bytes' write-back form (`Ed25519.cs:205–208`). |
| N4 Diff | FIXED. One internal birth, a get-only record, one `switch` capture rule. |
| `formal/reader.cs:571` | FIXED (`EntryType`, made once). |
| `Unreadable` | FIXED. Holds the Error whole as `list` cause (`app/this.cs:418–447`). |
| variable.list dead members | FIXED. `GetChangedSince`, `GetNames`, `Save`, `Restore`, `Clone`, `ToDictionary` gone; `GetAll`, `system.All` and `Names` have production callers (`Fluid.cs:183`, `this.cs:364`). |
| smaller items | FIXED: stale/orphan comments (`step/this.cs`, `goal/this.cs`, `action/list`, `serializer/Reader.cs`, `snapshot/this.cs`, `snapshot/serializer/Reader.cs`, `actor/context`, `call/this.cs`), `step.Start`'s `var result`, `Data.Error` the property itself, `Destination` → `Route`. |
| sqlite `GetAll` | FIXED, OK. A row that doesn't read is answered holding why (`store/sqlite/this.cs:373–375`); the setting holds it as a `SettingsUnreadable` error with the read's error as its cause (`actor/setting/this.cs:39–47`). |

## Findings (new, born in the fixes)

**S1 [raw hand-off] a view's name is written ×3 and read once, by the signature, with no owner.** `View.ToString().ToLowerInvariant()` at `signature/this.cs:93` (`Get("view")`), `:249` (the write) and `signature/this.Wire.cs:40` (the signed bytes); `data/schema/signature.cs:135–140` `Named` finds a view by comparing `ToString()` over a hand-listed four views. A new view is a fifth edit in the reader and, if it forgets the list, a signature that names it is refused. The discipline is the view's: `View.Name` (lowercase, once) and `View.Named(string)` beside the views, the four sites then say `View.Name` / `View.Named(text)`. Local to the signature, three call sites collapse, and it is not on a locked surface unless `View` is (it is internal/abstract in `app/`; the architect decides whether a member on it is Ingi's).
**Low, take with S1 or leave:** `type/reader/this.cs:526–559` `Reader` calls `Registered` (which runs `Narrowed`) and, on a miss, `Narrowed` again for the `Record`: one lookup done twice on the fallback path.

## Verdict
**FAIL, one small item (S1); coder next.** Everything else in 153 is done and the touched files are otherwise clean. Mechanical: if the architect rules S1 an Ingi-level addition on `View`, I will take it as ruled and PASS.
