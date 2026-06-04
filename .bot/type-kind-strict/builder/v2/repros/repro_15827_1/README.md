# Deterministic repro bundle — builder self-build failure

**Error:** Cannot convert System.String to app.module.llm.LlmMessage. Source value: 
**Exit:** 1
**Commit:** c472ca3d6

## Replay (deterministic — replays the cached LLM response, no live LLM)
1. Restore the caches that hold the bad response:
   - cp db/os.system.sqlite        <repo>/os/.db/system.sqlite
   - cp db/os.system.system.sqlite <repo>/os/system/.db/system.sqlite
   - (optional) cp app.pr <repo>/os/.build/app.pr
2. From <repo>/os run, with cache ON so the stored response replays:
   plang '--build={"files":["system/builder/Build.goal","system/builder/BuildGoal.goal","system/builder/BuildGoal/Start.goal","system/builder/BuildGoal/Plan.goal","system/builder/BuildGoal/Validate.goal","system/builder/BuildGoal/LlmFixer.goal","system/builder/BuildStep/Start.goal","system/builder/BuildStep/Validate.goal"],"cache":true}'
3. It reproduces the same error. Fix the handler, re-run step 2 to validate.

build.log holds the full failing output (stack trace + the failing step/goal).
