#!/usr/bin/env bash
# fast-apps' benchmark: the four measurements every stage is accepted against (test/plan/fast-apps/start.md,
# "What tests can't show"). Run from anywhere:
#   .bot/fast-apps/tester/bench/run.sh <label> [rounds]      (BENCH_MODES="root child" from stage 2 on)
# Each round, back to back: an app's birth (Debug and Release), a request app born 3,000 times (Debug and
# Release), the C# sweep (./dev.sh test --force), and plang --test from test/. The load is written beside every
# number. Results append to .bot/fast-apps/tester/bench/results/<label>.txt.
# Bench.cs is copied into the Types suite for the run and removed after; nothing else in the tree changes.
set -euo pipefail
here="$(cd "$(dirname "$0")" && pwd)"
repo="$(cd "$here/../../../.." && pwd)"
label="${1:?usage: run.sh <label> [rounds]}"
rounds="${2:-3}"
out="$here/results/$label.txt"
mkdir -p "$here/results"
copy="$repo/PLang.Tests/Types/FastAppsBench.cs"
cp "$here/Bench.cs" "$copy"
trap 'rm -f "$copy"' EXIT
cd "$repo"

load() { cut -d' ' -f1 /proc/loadavg; }
# the machine is shared: each measurement waits (up to 20 min) for the 1-minute load to fall under LOAD_MAX
# (default 3), so every number is taken at the same, low load; the load it started at is written beside it
calm() { local waited=0; while [ "$(cut -d. -f1 /proc/loadavg)" -ge "${LOAD_MAX:-3}" ] && [ $waited -lt 1200 ]; do sleep 15; waited=$((waited + 15)); done; }
now() { echo $(( $(date +%s%N) / 1000000 )); }
since() { local ms=$(( $(now) - $1 )); printf '%d.%d' $((ms / 1000)) $((ms % 1000 / 100)); }

echo "=== $label  $(git rev-parse --short HEAD)  $(date -u +%FT%TZ)  $(nproc) cpus  rounds $rounds" >> "$out"

./dev.sh build > /dev/null
dotnet msbuild PLang.Tests/Types/PLang.Tests.Types.csproj -t:Build -p:Configuration=Release -p:RunAnalyzers=false -v:q -nologo > /dev/null

bench() { # $1 = configuration, $2 = the test
  calm
  BENCH_ROOT="$repo/test" BENCH_OUT="$out" BENCH_LABEL="r$r $1" \
    "PLang.Tests/Types/bin/$1/net10.0/PLang.Tests.Types" --treenode-filter "/*/*/FastAppsBench/$2" < /dev/null > "$here/results/last-bench.log" 2>&1 \
    || { echo "r$r $1 $2 FAILED — see results/last-bench.log" >> "$out"; }
}

for r in $(seq 1 "$rounds"); do
  # BENCH_MODES="root child" measures each app twice: as a root and as a child made through the door (stage 2 on)
  for mode in ${BENCH_MODES:-root}; do
    export BENCH_MODE=$mode
    bench Debug Birth
    bench Release Birth
    bench Debug RequestApps
    bench Release RequestApps
  done

  calm; l=$(load); t=$(now)
  ./dev.sh test --force > "$here/results/last-sweep.log" 2>&1 || true
  echo "r$r sweep  load $l  wall $(since "$t") s  $(grep -E '^=== ' "$here/results/last-sweep.log" | sed -E 's/ *\(full output[^)]*\)//; s/=== ([A-Za-z]+) === /\1 /' | tr '\n' ';')" >> "$out"

  calm; l=$(load); t=$(now)
  (cd test && ../PlangConsole/bin/Debug/net10.0/plang --test < /dev/null > "$here/results/last-ptest.log" 2>&1) || true
  echo "r$r ptest  load $l  wall $(since "$t") s  $(grep -aoE '^  \[(Pass|Fail|Stale|Timeout|Skipped)\]' "$here/results/last-ptest.log" | sort | uniq -c | tr -s ' ' | tr '\n' ' ')  fail: $(grep -aE '^  \[(Fail|Timeout)\]' "$here/results/last-ptest.log" | cut -d' ' -f4 | tr '\n' ' ')" >> "$out"
done
echo "→ $out"
