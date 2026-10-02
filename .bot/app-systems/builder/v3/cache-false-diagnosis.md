# `cache:false` no longer rebuilds an unchanged goal — diagnosis (2026-10-02)

The architect asked why a full `os/ cache:false` build rebuilt 0 of 15 when the educator had seen
`cache:false` rebuild an unchanged goal. Answer: **on app-systems head, `cache:false` does NOT
rebuild an unchanged goal — in any folder.** It is a current regression, reproducible, not a
`/system/`-only effect.

## Repro (user folder, the educator's own `hash-take`)
```
cd /shared/educator/work/hash-take    # TYPESAFE_API_KEY from the secret
rm -rf .build .db .data
plang build '--build={"cache":false}' '--app={"create":true}'   # Build 1: Saved Start, .pr md5 = X
plang build '--build={"cache":false}'                           # Build 2: "Found 1 goals", NO rebuild, md5 = X (unchanged)
```
Build 2 skips the unchanged goal. (My os/ full build was the same: "Found 15 goals", rebuilt 0.)

## Mechanism (file:line)
1. `Executor.cs:105` applies `--build={"cache":false}` into the typed build setting
   (`Flag<build.setting>("!build")`), and `Executor.cs:111-112` reads it to turn the **LLM** cache
   off. **That half works** — fresh LLM calls still happen.
2. **CORRECTION (coder, after this was first written):** `%!build.setting.cache%` does **not** read
   undefined. My `--debug` watch was misleading — the debug variable watch reduced a watched name to
   its root (`!build`) and read only the store (`debug/this.cs:344`, `:351`), so **every** watched
   setting printed `(undefined)`. With the watch fixed, `%!build.setting.cache%` reads `false` with
   `--build={"cache":false}` and `true` without, at every Build.goal step. So do not trust the
   "(undefined)" evidence below; the precise clobber mechanism is the coder's (`set default` meeting a
   setting). What holds: the `set default` line was a **redundant** second copy of the class default.
3. `os/system/builder/Build.goal:7` — `- set default %!build.setting.cache% = true` — duplicated the
   class default (`build/setting/this.cs:10` `Cache = true`); removing it is what let cache-off rebuild.
4. `PLang/app/module/build/code/Default.cs:116`
   `if (context.Setting.Of<build.setting>().Cache.Value) await MergePrData(goal, context);` now reads
   **true** → merges the prior `.pr` → sets `goal.Cache` / each step's `PriorText`.
5. `PLang/app/goal/this.cs:288` `IsCached => Cache != null && Cache.Hash == Hash && … && Step.IsCached`
   (step: `goal/step/this.cs:35` `PriorText != null && PriorText == Text && Code.Count > 0`) → true for
   an unchanged goal → `BuildGoal/Start.goal` `- if %goal.IsCached%, return %goal.Cache%` skips it.

Net: `set default` (step 3) clobbers the CLI's `cache:false` before `Default.cs:116` (step 4) reads
it, so the merge runs and the unchanged goal is skipped. The clobber happens because the CLI value
isn't visible as `%!build.setting.cache%` at goal start (step 2).

## Why the educator saw it rebuild, and we don't now
`os/system/builder/Build.goal` is byte-identical on `app-systems` and `educator-lesson`, and the
settings refactor `7c98a5e44` (Sep 30, "a setting belongs to what it configures … .setting"; touches
`Executor.cs`, `app/actor/setting/this.cs`, `app/module/variable/set.cs`, `build/setting/this.cs`) is
in **both** the educator's `93046d681` (Oct 2 04:09) and head. So the regression is a commit on
`app-systems` **after** `93046d681`: on the educator's tree the CLI build-cache value still surfaced
as `%!build.setting.cache%` (so `set default` didn't override), and something since broke that
projection. Candidate area: how the CLI-applied `build.setting` (Executor `Flag`) is projected to the
`%!x.setting%` variable the goal reads — i.e. the `.setting` read path from `7c98a5e44`'s family, or a
follow-up to it. (Not pinned to a single later commit yet; the architect may want a bisect.)

## Is build.md 69-71 wrong?
No. Lines 69-71 describe the **source-unchanged skip** accurately ("its saved code stands … decided
before any LLM call"). They don't claim `cache:false` overrides it. The broken expectation is the v2
summary's / 414a1cfa3's "cache:false rebuilds an unchanged goal in full" — which **is** the intended
behavior (Default.cs:116 is written for it: "cache:false means no cached answer of any kind, so a
fresh source rebuilds in full — skip the merge entirely") and is currently defeated by the Build.goal
clobber. So it's a **code regression**, not a doc error. A one-line doc note could warn until fixed.

## FIX LANDED (builder half) — architect-directed, validated

The architect's call: the setting class already owns the default —
`PLang/app/module/build/setting/this.cs:10` `[Out, Store] public @bool Cache { get; set; } = true;`.
So `Build.goal:7`'s `set default %!build.setting.cache% = true` was a redundant second copy of that
default; removing it is what let cache-off rebuild. **Deleted that line; rebuilt `Build.goal`'s `.pr`
(bootstrap: cwd=os/, files=["system/builder/Build.goal"], cache:false).**

**Validated on hash-take** (fresh build, then rebuild the unchanged goal with cache:false):
- Before: Build 2 printed only "Found 1 goals" — skipped (no rebuild).
- After: Build 2 prints "Building goal: Start / Saved Start (8.0s)" and the `.pr` mtime changes — it
  **rebuilds**. The md5 is byte-identical because the rebuild is *deterministic* for unchanged source
  (same input → same `.pr`); that is the desired outcome, not a skip. (The architect's "md5 changes"
  expectation assumed a non-deterministic re-answer; an unchanged goal re-answers identically.)

**No separate core bug (corrected):** my first draft claimed `%!build.setting.cache%` read undefined
and a `.setting` projection bug owed to the coder — that was based on the bogus debug watch and is
**withdrawn**. The coder confirms the setting reads correctly (false with the flag, true without) at
every Build.goal step. The one needed change was removing the redundant `set default` — done. The
exact reason a redundant `set default = true` overrode the CLI's `false` (how `set default` interacts
with an already-set setting) is the coder's domain; it is not a `%!…setting%`-reads-undefined bug.

## Historical (superseded) fix options
These were written under the mistaken undefined-read reading; kept only for the record, not to act on:
- ~~**Core (settings projection):** make the CLI-applied `build.setting.cache` surface as
  `%!build.setting.cache%` …~~
- ~~**Builder lane (Build.goal), weaker:** have Build.goal consult the typed setting rather than
  `set default` on the variable.~~

## Impact on measurement
Fresh folders (`rm -rf .build`) always rebuild because there is no `.pr` to merge, so the prior
in-place skip did **not** affect fresh-folder measurements (c4, loop-dict, modules-probe this session
were all fresh — valid). With the redundant `set default` gone, cache-off now rebuilds an unchanged
tree in place too (validated on hash-take).
