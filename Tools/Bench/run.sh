#!/usr/bin/env bash
# What the runtime costs, in one run — and one row in Tools/Bench/record.md. Run it at every review: a sudden change shows.
#
#   Tools/Bench/run.sh                     everything below (~25 min: the rests alone are 10)
#   BENCH_SKIP="build sweep" Tools/Bench/run.sh      leave parts out: build sweep ptest birth request
#   BENCH_REST=0                           no rest before the last heap count (default 300 s; the sqlite pool lets go on a timer)
#   BENCH_LABEL=stage5                     a name beside the commit in the row
#   LOAD_MAX=3                             each measurement waits (≤ 20 min) for the 1-minute load to fall under this
#
# Measured, each started under LOAD_MAX, its load read at the start:
#   birth    a sub app's (made through the door, `new app.@this(path, actor)`) and a root's (as the command line makes it):
#            median time and bytes, birth + dispose
#   request  a request app × 3,000 (born, a goal loaded and run, disposed): time and bytes each, held MB at 0 / 1k / 2k / 3k
#            and after the rest — flat is no leak
#   ptest    `plang --test` from test/: wall time and the pass/fail counts
#   sweep    the C# suites, `./dev.sh test --force`: wall time and the suites red
#   build    the C# build, clean (every project rebuilt) and incremental (one internal file of PLang touched)
#
# The row's numbers are read against the noise bands at the top of record.md. The tree is left as found: the
# incremental build touches a file's time, not its text.
set -uo pipefail
unset CreateSymbolicLinksForCopyLocalIfPossible CreateHardLinksForAdditionalFilesIfPossible
here="$(cd "$(dirname "$0")" && pwd)"
repo="$(cd "$here/../.." && pwd)"
cd "$repo"
skip=" ${BENCH_SKIP:-} "
rest="${BENCH_REST:-300}"
work="$here/results"; mkdir -p "$work"
want() { [[ "$skip" != *" $1 "* ]]; }

# the machine is shared: wait for the load to fall; the highest load a measurement started at goes in the row
maxload=0.00
calm() {
  local waited=0
  while [ "$(cut -d. -f1 /proc/loadavg)" -ge "${LOAD_MAX:-3}" ] && [ $waited -lt 1200 ]; do sleep 15; waited=$((waited + 15)); done
  local l; l=$(cut -d' ' -f1 /proc/loadavg)
  [ "$(( 10#${l/./} ))" -gt "$(( 10#${maxload/./} ))" ] 2>/dev/null && maxload=$l
  load=$l
}
now() { echo $(( $(date +%s%N) / 1000000 )); }
# the end of a single-threaded measurement (birth, request) also reads the load: another session's build started mid-run
# shows in the row (the 1-minute load lags, so it is a floor). Not after a build, the sweep or plang --test: they load
# the machine themselves (~8 on 8 cpus), so an end load there says nothing about a neighbour.
since() { local ms=$(( $(now) - $1 )); printf '%d.%d' $((ms / 1000)) $((ms % 1000 / 100)); }
peek() {
  local l; l=$(cut -d' ' -f1 /proc/loadavg)
  [ "$(( 10#${l/./} ))" -gt "$(( 10#${maxload/./} ))" ] 2>/dev/null && maxload=$l
}
field() { grep -oE "(^| )$2=[^ ]+" <<<"$1" | head -1 | cut -d= -f2; }   # field "<line>" key

DEVFLAGS=(-p:Configuration=Debug -p:RunAnalyzers=false -v:q -nologo)
row_build_clean="-"; row_build_incr="-"
if want build; then
  # clean: every project's outputs removed (not timed), then dev.sh's build, from nothing, is the number
  for f in PLang.Generators/*.csproj PLang/*.csproj PLang.Tests/*/PLang.Tests.*.csproj PlangConsole/PLangConsole.csproj; do
    dotnet msbuild "$f" -t:Clean "${DEVFLAGS[@]}" > /dev/null 2>&1
  done
  calm; t=$(now)
  ./dev.sh build > "$work/last-build-clean.log" 2>&1
  [ $? -eq 0 ] && row_build_clean="$(since "$t") s" || row_build_clean="FAILED"
  # one internal file's time moves; PLang compiles again, what is built on its public surface does not
  touch "$repo/PLang/app/test/timing/this.cs"
  calm; t=$(now)
  ./dev.sh build > "$work/last-build-incr.log" 2>&1
  [ $? -eq 0 ] && row_build_incr="$(since "$t") s" || row_build_incr="FAILED"
fi

# the tool, built Release once (after the build above: the Debug build is what the sweep and plang --test run)
bench="$here/bin/Release/net10.0/PLang.Tests.Bench"
if want birth || want request; then
  dotnet build "$here/Bench.csproj" -c Release -v:q -nologo > "$work/last-bench-build.log" 2>&1 || { echo "Bench did not build — $work/last-bench-build.log"; exit 1; }
fi
export BENCH_ROOT="$repo/test"

b_sub="-"; b_root="-"; r_sub="-"; r_root="-"; h_sub="-"; h_root="-"
if want birth; then
  for m in child root; do
    calm; out=$("$bench" birth $m 2>"$work/last-birth.err" < /dev/null | tail -1)
    v="$(field "$out" ms) ms, $(field "$out" kb) KB"
    [ $m = child ] && b_sub="$v" || b_root="$v"
    peek
  done
fi
if want request; then
  for m in child root; do
    calm; out=$(BENCH_REST=$rest "$bench" request $m 2>"$work/last-request.err" < /dev/null | tail -1)
    v="$(field "$out" ms) ms, $(field "$out" kb) KB"; held="$(field "$out" held)"
    held="${held//>/ → }"; [ "$(field "$out" failed)" = 0 ] || held="$held (failed $(field "$out" failed))"
    if [ $m = child ]; then r_sub="$v"; h_sub="$held"; else r_root="$v"; h_root="$held"; fi
    peek
  done
fi

p_test="-"
if want ptest; then
  calm; t=$(now)
  (cd test && ../PlangConsole/bin/Debug/net10.0/plang --test < /dev/null > "$work/last-ptest.log" 2>&1)
  w=$(since "$t")
  c() { grep -aEc "^  \[$1\]" "$work/last-ptest.log"; }
  p_test="$w s ($(c Pass) pass, $(c Fail) fail)"
fi

s_sweep="-"
if want sweep; then
  calm; t=$(now)
  ./dev.sh test --force > "$work/last-sweep.log" 2>&1
  red=$(grep -aE '^=== .* FAILED' "$work/last-sweep.log" | grep -aoE '^=== [A-Za-z]+' | cut -c5- | tr '\n' ',' )
  red="${red%,}"
  s_sweep="$(since "$t") s (red: ${red:-none})"
fi

# the row goes in under the marker, newest first
branch=$(git branch --show-current); commit=$(git rev-parse --short HEAD)
dirty=""; [ -n "$(git status --porcelain --untracked-files=no -- PLang PlangConsole PLang.Generators os | head -1)" ] && dirty="+dirty"
label="${BENCH_LABEL:-}"
row="| $(date -u +%F) | $branch | $commit$dirty${label:+ ($label)} | $maxload | $b_sub | $b_root | $r_sub | $r_root | $h_sub | $h_root | $p_test | $s_sweep | $row_build_clean | $row_build_incr |"
record="${BENCH_RECORD:-$here/record.md}"
n=$(grep -n '^<!-- rows -->' "$record" | head -1 | cut -d: -f1)
{ head -n "$n" "$record"; echo "$row"; tail -n +$((n + 1)) "$record"; } > "$record.new" && mv "$record.new" "$record"
echo "$row"
