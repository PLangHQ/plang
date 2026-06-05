# Tester summary — collections-are-data

**Version:** v7 (matches coder v7) · **Verdict: PASS**

## What this is

`collections-are-data` makes PLang collections first-class `Data` (native `dict`/`list`
value types, set-rebinds, one typed-compare path, list/dict ops incl. new `where`, `item`
apex, row/chunk list model, `@schema:data` wire marker). v7 resolves my v6 FAIL.

## The v6 → v7 story

- **v6 FAIL:** two signing regression tests (green at base) were disabled by **gutting**
  them into no-op `write out '...disabled'` steps that still **passed** and counted in
  273/273 — masking a live regression (verify of a signed value through a list /
  across a goal call is broken). I proved it by restoring the real test → red on the
  branch binary.
- **v7 fix (verified honest):** new `test.discover.HasSkipTag` reads a
  `- tag this test 'skip'` step from goal **source text** and short-circuits to
  `Status.Skipped` **before** the build/`.pr` check. The two goals hold their **real
  steps again** (parked, not gutted; re-enable = delete the tag line). Suite now reports
  **271 pass + 2 skipped + 0 fail** — honest.

## What was done (this session)

- Clean rebuild; **C# 4089/4089**.
- plang: confirmed **271 pass + 2 skipped + 0 fail** across multiple isolated runs, git
  clean. Both signing goals register `[Skipped]`, not Pass.
- **Scope / over-match check:** exactly the two intended goals are skipped. A non-`skip`
  tag goal (`tag this test 'http'`, `'fast','slow'`) still **runs and passes** — live
  proof the `^...'skip'...$` regex doesn't over-match.
- Confirmed both goals contain their real `sign → … → verify → assert` steps; the
  signing regression is genuinely still deferred (signature-as-schema-wrapper), merge
  gate intact.
- One flaky `timeout` appeared on a timing-sensitive enforce-timeout test while the C#
  suite ran concurrently; cleared on every isolated run (CPU contention, not a branch
  bug).

## Findings (minor — none block)

1. **`HasSkipTag` has no regression test.** It's the new integrity mechanism; if the
   regex is later broadened it could silently skip tests (future false-green). Add two
   C# tests in `DiscoverActionTests`: a `'skip'`-tagged goal → Skipped; a near-miss
   `'slow'` tag → NOT skipped. Empirically safe today.
2. **`where` action still lacks a plang test** (carried from v3).

## Code example — the honest park (real steps, just tagged)

```
Start
- tag this test 'skip'
/ Skipped pending the signature rework ... remove the 'skip' tag to re-enable.
- sign "hello world", write to %signed%
- add %signed% to %list%
- verify %list[0]%, write to %ok%
- assert %ok% equals true          # ← real assertion, parked — registers Skipped, not Pass
```

## Next

`run.ps1 security collections-are-data "Review the code on branch collections-are-data" -b collections-are-data`
