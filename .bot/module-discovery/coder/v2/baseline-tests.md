# Baseline — module-discovery @ c2f674647 (before Stage 4 spike)

Captured via `./dev.sh full` (single run — logs kept in `baseline-logs/`, names in `baseline-fails.txt`).

## C# suites — 194 reds (193 unique names)
| Suite | Total | Failed |
|---|---|---|
| Modules | 796 | 33 |
| Types | 718 | 28 |
| Wire | 472 | 25 |
| Data | 886 | 47 |
| Generator | 198 | 7 |
| Runtime | 704 | 54 |

Matches the plan's stated ~195-red baseline. These are pre-existing (the Stage-4 stabilization tail). **Any green→red here after the spike is MY regression**; diff by name against `baseline-fails.txt` (`comm -13`), never re-run-and-count (count is flaky).

## plang `--test`
`plang --test` errors during `/system/.build/test.pr` materialize: `MaterializeFailed(400) — value slot 'Name' has no declared type. Value was: %path%`. Pre-existing (system .pr not rebuilt on this branch). Not chasing — not spike scope.

## Spike acceptance (what must stay true)
- No new C# reds vs `baseline-fails.txt`.
- The 5 spike legs demonstrably work (or a leg's failure is surfaced with the exact reason — leg (d) async prose is the least-proven).
