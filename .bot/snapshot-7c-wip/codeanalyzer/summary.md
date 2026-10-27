# codeanalyzer summary — snapshot-7c-wip

**Version:** v3 (narrow third check, `review3.md`): all of 153's items are done; one small finding born in the fix (S1: a view's name written ×3 in the signature, found by a hand-listed four; the view should own `Name`/`Named`). FAIL on that alone, coder next. Below is v2.

**v2** (re-check at 23d61537b). Verdict: FAIL, coder next.

**What this is:** the full OBP review of the snapshot stage 7c wip. v1 (at d119e797f) found 36 items; v2 re-checks them at HEAD after the coder's fixes and the architect's rulings (123–151).

**What was done:** three readers read the 82 files changed since d119e797f; I re-read item 1 and the View door. Result in `review2.md`: most items fixed or Ingi's; the not-Ingi's remainder is item 1 (the signature's `text? View` beside `Origin`), N1 (`View.Reader` and `View.Make` look a reader up twice), the Diff's two births, a per-entry `new Regex` in the formal reader, `Unreadable` cracking Errors, dead members of `variable.list`, and stale comments. Bench (birth, request, ptest, sweep): all four byte figures are under the base d72538402 (root birth 2312 KB, sub request 529 KB, root request 3785 KB), so there is nothing to name or make lazy. Held memory flat.

**Next:** the coder fixes the "still to fix" list in `review2.md`; Ingi's list stays his. Then a short re-check of item 1 and N1.

**Example (item 1):**
```csharp
// now: the signature holds the view as text, beside Origin, and turns it back by comparing text
public text? View { get; }
internal View Hashed => new[] { View.Store, View.Out, View.Snapshot }.FirstOrDefault(v => v.ToString() == View?.ToString()) ?? (Origin == Stored ? View.Store : View.Out);
// shape: the signature holds an app.View set at birth; the reader parses the name once; Origin follows or goes
```
