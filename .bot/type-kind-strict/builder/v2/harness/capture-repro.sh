#!/usr/bin/env bash
# Capture a DETERMINISTIC repro bundle for a builder self-build failure, for coder.
#
# Insight: the llmcache replays a stored LLM response byte-for-byte (this is what
# poisoned us earlier). So if we run a build with cache ON, the random nano response
# that triggered a failure gets CACHED — and from then on the failure is deterministic.
# The bundle = app snapshot (app.pr) + the cache dbs (with the bad response) + the
# exact command + the observed error. Coder restores the dbs/app, runs the command with
# cache:true, and reproduces the SAME error every run. No LLM, fast, deterministic.
#
# Usage: capture-repro.sh [maxRuns]   (default 6 — keeps building until a failure caches)
set -uo pipefail
HERE="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO="$(cd "$HERE/../../../../.." && pwd)"
OSDIR="$REPO/os"
PLANG="$REPO/PlangConsole/bin/Debug/net10.0/plang"
REPROS="$HERE/../repros"; mkdir -p "$REPROS"
MAX="${1:-6}"
FILES='["system/builder/Build.goal","system/builder/BuildGoal.goal","system/builder/BuildGoal/Start.goal","system/builder/BuildGoal/Plan.goal","system/builder/BuildGoal/Validate.goal","system/builder/BuildGoal/LlmFixer.goal","system/builder/BuildStep/Start.goal","system/builder/BuildStep/Validate.goal"]'

clear_cache(){ python3 - <<'PY'
import sqlite3,os
for db in ['os/.db/system.sqlite','os/system/.db/system.sqlite']:
    if os.path.exists(db): c=sqlite3.connect(db); c.execute('DELETE FROM llmcache'); c.commit(); c.close()
PY
}

# Start clean so the FIRST failing fresh response is the one that caches.
( cd "$REPO" && clear_cache )

for i in $(seq 1 "$MAX"); do
  echo "run $i (cache ON — bad responses will cache)…"
  log="$REPROS/.last_run.log"
  ( cd "$OSDIR" && "$PLANG" "--build={\"files\":$FILES,\"cache\":true}" ) >"$log" 2>&1
  rc=$?
  if grep -qa "ValidationErrors\|DeserializationFailed\|NullReferenceException\|not found\|not valid JSON\|Cannot convert" "$log"; then
    err="$(grep -aE "Reason:" -A1 "$log" | grep -avE "Reason:|^--" | head -1 | sed 's/^[[:space:]]*//' | cut -c1-100)"
    [ -z "$err" ] && err="$(grep -aoE '(DeserializationFailed|NullReferenceException|Cannot convert[^\"]*|not valid JSON|Action .* not found)' "$log" | head -1)"
    id="repro_$(grep -ac '' "$log")_$i"
    d="$REPROS/$id"; mkdir -p "$d/db"
    cp "$REPO/os/.db/system.sqlite" "$d/db/os.system.sqlite" 2>/dev/null
    cp "$REPO/os/system/.db/system.sqlite" "$d/db/os.system.system.sqlite" 2>/dev/null
    cp "$REPO/os/.build/app.pr" "$d/app.pr" 2>/dev/null
    cp "$log" "$d/build.log"
    cat > "$d/README.md" <<EOF
# Deterministic repro bundle — builder self-build failure

**Error:** $err
**Exit:** $rc
**Commit:** $(git -C "$REPO" rev-parse --short HEAD)

## Replay (deterministic — replays the cached LLM response, no live LLM)
1. Restore the caches that hold the bad response:
   - cp db/os.system.sqlite        <repo>/os/.db/system.sqlite
   - cp db/os.system.system.sqlite <repo>/os/system/.db/system.sqlite
   - (optional) cp app.pr <repo>/os/.build/app.pr
2. From <repo>/os run, with cache ON so the stored response replays:
   plang '--build={"files":$FILES,"cache":true}'
3. It reproduces the same error. Fix the handler, re-run step 2 to validate.

build.log holds the full failing output (stack trace + the failing step/goal).
EOF
    echo "CAPTURED -> $d  ($err)"
    git -C "$REPO" checkout -- os/system/builder/.build os/system/builder/BuildGoal/.build os/system/builder/BuildStep/.build 2>/dev/null
    exit 0
  fi
  git -C "$REPO" checkout -- os/system/builder/.build os/system/builder/BuildGoal/.build os/system/builder/BuildStep/.build 2>/dev/null
done
echo "no failure captured in $MAX runs (builds succeeded — try more runs)"