# Test baseline — `wire-source-split` pre-implementation

Captured at commit **`b75e7c76e`** (branch tip before any code change), 2026-07-12.
Establishes the red set so regressions are told from pre-existing failures BY NAME
(counts are flaky — diff names). C# suites only (`./dev.sh`); plang `--test` is not
run on this branch (does not work in the current state).

> **AUTHORITATIVE baseline is `union/` (2026-07-13).** The single-run `<Suite>.txt`
> lists below are **flaky-low** — a single `dev.sh full` of a flaky suite records a
> coin-flip red-set (Wire recorded 10; the true parent red-set is 31; Runtime 14 vs 65).
> Diffing against them manufactures false "new reds." Use `union/<Suite>_union.txt`
> (parent, ×2 runs unioned) and grep the kept `union/<Suite>_run*.log`. Verified:
> every candidate "new red" during implementation (Wire ×15, Runtime ×11, Types ×6,
> Data ×1, AsT, SettingsData) is in the union → **zero real regressions on the branch.**

## C# suites (`./dev.sh full`)

| Suite | Failing / Total |
|---|---|
| Modules | 102 / 656 |
| Types | 37 / 726 |
| Wire | 10 / 251 |
| Data | 57 / 854 |
| Generator | 8 / 198 |
| Runtime | 14 / 353 |
| **Total** | **228 red** |

Failing names per suite: `<Suite>.txt` (sorted, one name per line).

### How to diff after a change
```bash
# regenerate current failing names for a suite
grep -oE "^failed [A-Za-z0-9_]+" /tmp/devsh_<Suite>.log | sed 's/^failed //' | sort -u > /tmp/cur.txt
# NEW reds (regressions) — in current, not in baseline:
comm -13 .bot/wire-source-split/coder/baseline/<Suite>.txt /tmp/cur.txt
# NEWLY GREEN (fixed) — in baseline, not in current:
comm -23 .bot/wire-source-split/coder/baseline/<Suite>.txt /tmp/cur.txt
```

Goal through the branch: **zero entries in the NEW-reds column**; the 5 strict-image
reds + related materialization failures move to the NEWLY-GREEN column.
