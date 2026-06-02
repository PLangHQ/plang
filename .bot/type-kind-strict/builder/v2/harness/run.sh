#!/usr/bin/env bash
# Builder-prompt eval harness.
#
# Runs N cache:false builds of the corpus from the Tests/ dir, scores each run
# against the oracle (correct mapping, not just "didn't crash"), and aggregates:
#   pass-rate (the gate) | median completion tokens (cost) | median wall time.
#
# Usage:  run.sh [N]                 (default N=3)
# Env:    PLANG=../PlangConsole/bin/Debug/net10.0/plang   (override to test a build)
#
# All paths are resolved from this script's own location, so it can be run from
# anywhere. Builds always execute with cwd=Tests/ (the validated build cwd).
set -euo pipefail

HERE="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO="$(cd "$HERE/../../../../.." && pwd)"          # .bot/<b>/builder/v2/harness -> repo root
TESTS="$REPO/Tests"
PLANG="${PLANG:-$REPO/PlangConsole/bin/Debug/net10.0/plang}"
CORPUS="$HERE/corpus.json"
N="${1:-3}"
OUT="$HERE/results"
mkdir -p "$OUT"

# Pull the buildFiles array out of corpus.json as a JSON string for --build.
FILES_JSON="$(python3 -c "import json;print(json.dumps(json.load(open('$CORPUS'))['buildFiles']))")"

echo "harness: N=$N  plang=$PLANG"
echo "harness: corpus files=$FILES_JSON"
echo

RESULTS_FILE="$OUT/run_$(date +%s).jsonl"
: > "$RESULTS_FILE"

for i in $(seq 1 "$N"); do
  # snapshot trace dirs before, so we can identify the one this build creates
  before="$(ls "$TESTS/.build/traces" 2>/dev/null | sort || true)"
  start=$(date +%s.%N)
  ( cd "$TESTS" && "$PLANG" "--build={\"files\":$FILES_JSON,\"cache\":false}" ) >"$OUT/build_$i.log" 2>&1 || true
  end=$(date +%s.%N)
  elapsed=$(python3 -c "print(round($end-$start,1))")

  after="$(ls "$TESTS/.build/traces" 2>/dev/null | sort || true)"
  newdir="$(comm -13 <(echo "$before") <(echo "$after") | tail -1)"
  if [ -z "$newdir" ]; then
    # fallback: newest dir
    newdir="$(ls -t "$TESTS/.build/traces" | head -1)"
  fi
  tracedir="$TESTS/.build/traces/$newdir"

  score="$(python3 "$HERE/score.py" --pr-root "$TESTS" --corpus "$CORPUS" --trace-dir "$tracedir")"
  score="$(python3 -c "import json,sys;d=json.loads(sys.argv[1]);d['trial']=$i;d['seconds']=$elapsed;print(json.dumps(d))" "$score")"
  echo "$score" | tee -a "$RESULTS_FILE" | python3 -c "import json,sys;d=json.load(sys.stdin);print(f\"  trial {d['trial']}: pass={d['pass']} steps={d['casesPass']} calls={d['calls']} out_tokens={d['completion']} {d['seconds']}s cost=\${d['cost']}\")"
done

echo
echo "=== aggregate over $N trials ==="
python3 - "$RESULTS_FILE" <<'PY'
import json,sys,statistics
rows=[json.loads(l) for l in open(sys.argv[1]) if l.strip()]
passes=sum(1 for r in rows if r["pass"])
def med(k): return round(statistics.median([r[k] for r in rows]),1)
print(f"pass-rate (GATE) : {passes}/{len(rows)}")
print(f"median out tokens: {med('completion')}")
print(f"median calls     : {med('calls')}")
print(f"median seconds   : {med('seconds')}")
print(f"median cost      : ${med('cost')}")
# show any mapping misses
for r in rows:
    if not r["pass"] and r.get("perCase"):
        print(f"  [trial {r['trial']}] misses:", json.dumps(r["perCase"]))
PY
echo
echo "raw: $RESULTS_FILE"
