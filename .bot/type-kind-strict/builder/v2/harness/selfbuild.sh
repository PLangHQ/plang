#!/usr/bin/env bash
# Self-build eval harness — does the builder rebuild ITS OWN goals to the trusted oracle?
#
# Each trial: cache:false rebuild of the builder's own .goal files from os/system,
# score the produced .pr against the committed oracle, sum LLM usage, then
# RESTORE the builder .pr from git (critical: a flaky self-build overwrites the
# good .pr — without restore, trial N+1 runs on trial N's possibly-broken builder
# AND the repo gets corrupted).
#
# Usage:  selfbuild.sh [N]            (default N=3)
# Env:    PLANG=...   override the executable
set -uo pipefail

HERE="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO="$(cd "$HERE/../../../../.." && pwd)"
SYS="$REPO/os/system"          # where the builder .pr live (oracle / restore target)
OSDIR="$REPO/os"               # build cwd — MUST be os/ (NOT os/system), per building-the-builder.md
PLANG="${PLANG:-$REPO/PlangConsole/bin/Debug/net10.0/plang}"
N="${1:-3}"
OUT="$HERE/results"; mkdir -p "$OUT"
REF="$HERE/selfbuild_reference.json"
TRACES="$OSDIR/.build/traces"  # cwd=os/ -> /.build/traces resolves here

# Canonical recipe from building-the-builder.md: cwd=os/, files MUST start with
# system/builder/ (a bare/builder-prefixed filter silently pulls in dozens of
# unrelated goals -> phantom BuilderPlannerFailed). Entry first, leaves last.
FILES_JSON='["system/builder/Build.goal","system/builder/BuildGoal.goal","system/builder/BuildGoal/Start.goal","system/builder/BuildGoal/Plan.goal","system/builder/BuildGoal/Validate.goal","system/builder/BuildGoal/LlmFixer.goal","system/builder/BuildStep/Start.goal","system/builder/BuildStep/Validate.goal"]'

# The .pr (.build dirs) are the oracle and get restored each trial. They MUST be
# committed-clean so the restore is meaningful. Prompt/template edits (.llm/.md
# under builder, but NOT .build) are intentionally left live — that's what we test.
PR_DIRS=(os/system/builder/.build os/system/builder/BuildGoal/.build os/system/builder/BuildStep/.build)
if ! git -C "$REPO" diff --quiet -- "${PR_DIRS[@]}"; then
  echo "ABORT: builder .pr (.build) have uncommitted changes — commit/restore them so the oracle is the committed state." >&2
  exit 1
fi

# Capture the trusted oracle from the current (committed) .pr.
python3 "$HERE/selfbuild.py" ref --root "$SYS" --out "$REF"
echo

RESULTS="$OUT/selfbuild_$(date +%s).jsonl"; : > "$RESULTS"
echo "selfbuild: N=$N  plang=$PLANG"
echo

clear_cache() {
  # cache:false does NOT fully bypass the local llmcache on the build path, and a
  # single degenerate/empty response gets cached + replayed — poisoning every later
  # build. Clear it before each trial so we measure the MODEL, not stale cache.
  python3 - <<'PY'
import sqlite3, os
for db in ['os/.db/system.sqlite','os/system/.db/system.sqlite']:
    if os.path.exists(db):
        c=sqlite3.connect(db); c.execute('DELETE FROM llmcache'); c.commit(); c.close()
PY
}

for i in $(seq 1 "$N"); do
  ( cd "$REPO" && clear_cache )
  before="$(ls "$TRACES" 2>/dev/null | sort || true)"
  start=$(date +%s.%N)
  ( cd "$OSDIR" && "$PLANG" "--build={\"files\":$FILES_JSON,\"cache\":false}" ) >"$OUT/selfbuild_$i.log" 2>&1
  rc=$?
  end=$(date +%s.%N)
  elapsed=$(python3 -c "print(round($end-$start,1))")
  after="$(ls "$TRACES" 2>/dev/null | sort || true)"
  # ALL trace dirs created during this build (a self-build scatters several)
  mapfile -t newdirs < <(comm -13 <(echo "$before") <(echo "$after"))
  tdirs=(); for d in "${newdirs[@]}"; do [ -n "$d" ] && tdirs+=("$TRACES/$d"); done
  [ ${#tdirs[@]} -eq 0 ] && tdirs=("$TRACES/$(ls -t "$TRACES" | head -1)")

  score="$(python3 "$HERE/selfbuild.py" score --root "$SYS" --ref "$REF" --traces "${tdirs[@]}")"
  # Merge build exit code + extract the failing reason from the log. A non-zero
  # exit (or a validation error in the log) is a FAIL even when the .pr stayed
  # = oracle (the build aborts before overwriting the goal it choked on).
  score="$(python3 - "$score" "$i" "$elapsed" "$rc" "$OUT/selfbuild_$i.log" <<'PY'
import json,sys,re
d=json.loads(sys.argv[1]); d['trial']=int(sys.argv[2]); d['seconds']=float(sys.argv[3]); d['exit']=int(sys.argv[4])
log=open(sys.argv[5],errors='ignore').read()
m=re.search(r'Reason:\s*\n\s*(.+)', log)
gm=re.findall(r'(\w[\w/]*\.goal):(\d+)', log)
d['buildError']=(m.group(1).strip()[:120] if m else None)
d['failGoal']=(f"{gm[0][0]}:{gm[0][1]}" if (d['exit']!=0 and gm) else None)
d['buildOk']=(d['exit']==0 and 'ValidationErrors' not in log and 'Unhandled exception' not in log)
d['pass']=bool(d['pass'] and d['buildOk'])
print(json.dumps(d))
PY
)"
  echo "$score" | tee -a "$RESULTS" | python3 -c "import json,sys;d=json.load(sys.stdin);print(f\"  trial {d['trial']}: pass={d['pass']} buildOk={d['buildOk']} steps={d['matched']}/{d['total']} calls={d['calls']} out={d['completion']} {d['seconds']}s exit={d['exit']} divs={len(d['divergences'])}\"+(f\"  FAIL@{d['failGoal']}: {d['buildError']}\" if not d['buildOk'] else ''))"

  # RESTORE the trusted builder .pr before the next trial (ONLY .build — leave
  # prompt/template edits live so we keep testing them).
  git -C "$REPO" checkout -- "${PR_DIRS[@]}"
done

echo
echo "=== aggregate over $N trials ==="
python3 - "$RESULTS" <<'PY'
import json,sys,statistics
rows=[json.loads(l) for l in open(sys.argv[1]) if l.strip()]
passes=sum(1 for r in rows if r["pass"])
ok=[r for r in rows if r.get("buildOk")]
def med(k,rs=rows): return round(statistics.median([r[k] for r in rs]),1) if rs else 0
print(f"pass-rate (GATE)     : {passes}/{len(rows)}")
print(f"builds completed     : {len(ok)}/{len(rows)}")
print(f"median out tokens    : {med('completion',ok)}  (completed builds only)")
print(f"median calls         : {med('calls',ok)}")
print(f"median seconds (all) : {med('seconds')}")
# build failures = where the self-build aborted
fails={}
for r in rows:
    if not r.get("buildOk"):
        k=f"{r.get('failGoal')} :: {r.get('buildError')}"
        fails[k]=fails.get(k,0)+1
if fails:
    print("\nbuild failures (self-build aborted):")
    for k,c in sorted(fails.items(),key=lambda x:-x[1]): print(f"  [{c}/{len(rows)}] {k}")
# union of divergent (pr,goal,step) across trials = the flaky surface
flaky={}
for r in rows:
    for d in r.get("divergences",[]):
        if "goal" in d:
            k=f"{d['pr']}::{d['goal']}[{d['step']}]"
            flaky.setdefault(k,{"count":0,"want":d["want"],"got":[]})
            flaky[k]["count"]+=1
            if d["got"] not in flaky[k]["got"]: flaky[k]["got"].append(d["got"])
if flaky:
    print("\nflaky steps (diverged in >=1 trial):")
    for k,v in sorted(flaky.items(), key=lambda x:-x[1]["count"]):
        print(f"  [{v['count']}/{len(rows)}] {k}\n      want={v['want']}\n      got ={v['got']}")
else:
    print("\nno divergences — self-build reproduced the oracle in every trial.")
print(f"\nraw: {sys.argv[1]}")
PY
