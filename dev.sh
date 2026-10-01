#!/usr/bin/env bash
# Fast inner-loop build/test for PLang development.
#
# Usage:
#   ./dev.sh build              # incremental build of all test projects + PlangConsole (analyzers off); skipped when no source changed
#   ./dev.sh test [filter]      # build, then run C# tests; filter = test-class name (finds the right project), e.g. ./dev.sh test ReturnTests
#   ./dev.sh test               # a sweep: only the suites the C# tree's changes since the last complete sweep touch (none
#                               # when nothing changed — the stored results stand), at most 3 at once, niced
#   ./dev.sh test --force       # a sweep of every suite, changed or not (a flake, a machine that was overloaded)
#   ./dev.sh ptest              # build, then run plang tests (from test/)
#   ./dev.sh full               # the handoff gate, in its own Gate configuration: analyzers ON (PLNG001/PLNG002, TUnit warnings) + ALL suites + plang tests
#
# Every subcommand builds first — never `./dev.sh build && ./dev.sh test`, never loop `./dev.sh test`.
#   ./dev.sh warm               # background-friendly warmup; run once at session start (absorbs the after-idle stall)
#
# The C# tests live in per-area projects under PLang.Tests/ (Modules, Types,
# Wire, Data, Generator, Runtime + Shared helpers). An edit recompiles only its
# slice; slices build in parallel via PLang.Tests/All.proj.
#
# Why the flags matter (measured 2026-06-10, .bot/compare-redesign/coder/build-speed-report.md):
#   - dotnet run --project <tests>  → 90s+ per call (restore + eval + build + run). Never.
#   - -p:RunAnalyzers=false         → saves ~17s per test-project compile. MUST be consistent:
#     alternating the flag invalidates incremental state (full rebuild). Generators still run.
#   - Test binaries run directly; TUnit filter: --treenode-filter "/*/*/*Name*/*"
set -euo pipefail
cd "$(dirname "$0")"
export DOTNET_CLI_USE_MSBUILD_SERVER=1

DEVFLAGS=(-p:Configuration=Debug -p:RunAnalyzers=false -v:q -nologo)
PROJECTS=(Modules Types Wire Data Generator Runtime)

# --timeout is a WHOLE-SUITE cap (TUnit's "global test execution timeout"), not
# per-test. It exists only as a safety net: one test blocks on stdin (a stream/ask
# channel test reading input that never arrives) and hangs the suite forever — that
# was the 5-minute "Wire hang". The cap cancels it so the suite finishes.
#
# It must sit WELL ABOVE the slowest suite's real time, or it silently truncates:
# at 15s, Modules (21-27s) was cancelled mid-run EVERY run, reporting 500-737 of its
# ~1000 tests with the count varying by CPU load — untested failures vanished and
# "failed: N" meant nothing. A hang is minutes, so a generous cap still catches it
# immediately; the cap is a net, not a budget. The suites run in PARALLEL, where the slowest
# (Modules) takes ~85s under shared load (2026-09-28) — keep the cap well above that. Niced under a
# saturated machine (load ~50, 2026-09-30) Types alone took over 4 minutes and Modules ~6, so the cap is 600s.
# Override via TEST_TIMEOUT=Ns.
TEST_TIMEOUT="${TEST_TIMEOUT:-600s}"

run_bin() { # $1 = configuration (Debug | Gate), $2 = project, rest = args
  # < /dev/null: a test that reads stdin (a stream/ask-channel test) otherwise BLOCKS
  # waiting for input until the whole-suite --timeout cap fires — turning a few-second
  # run into a multi-minute hang. EOF on stdin lets it fail fast instead.
  # nice: the machine is shared (Windows, terminals, other bots) — tests yield to them.
  nice -n 10 "PLang.Tests/$2/bin/$1/net10.0/PLang.Tests.$2" --timeout "$TEST_TIMEOUT" "${@:3}" < /dev/null
}

# At most this many suites run at once — six at once took all 8 cores of the shared machine.
DEVSH_PARALLEL="${DEVSH_PARALLEL:-3}"

# --- No change, no sweep ---
# A complete sweep stores a snapshot of the C# tree: HEAD, then each file that differs from HEAD or is
# untracked under the C# roots, with its content hash — and each suite's summary line. The next sweep
# compares: the same snapshot runs nothing (the stored lines are the answer); changes only under
# PLang.Tests/<Suite>/ run only those suites; anything else in the C# tree runs them all.
CS_ROOTS=(PLang PLang.Generators PlangConsole PLang.Tests)
SWEEP=.devsh-sweep
snapshot() {
  git rev-parse HEAD
  { git diff --name-only HEAD -- "${CS_ROOTS[@]}"; git ls-files --others --exclude-standard -- "${CS_ROOTS[@]}"; } \
    | grep -vE '/(bin|obj)/' | sort -u | while read -r f; do
      if [ -f "$f" ]; then echo "$f $(git hash-object "$f")"; else echo "$f deleted"; fi
    done
}
# The files changed since the stored snapshot (one per line): the commits between the two HEADs, and
# every file whose (path, hash) line differs between the snapshots.
changed_since_sweep() {
  local old_head new_head
  old_head=$(head -1 "$SWEEP"); new_head=$(git rev-parse HEAD)
  # `|| true`: nothing changed is an empty answer, not a failure (set -e + pipefail would end the script)
  {
    if [ "$old_head" != "$new_head" ]; then git diff --name-only "$old_head" "$new_head" -- "${CS_ROOTS[@]}" 2>/dev/null || true; fi
    diff <(tail -n +2 "$SWEEP") <(snapshot | tail -n +2) | grep -E '^[<>] ' | sed -E 's/^[<>] //; s/ [^ ]+$//' || true
  } | sort -u
}
# The suites a set of changed files touches: every suite when any change is outside PLang.Tests/<Suite>/
# (the library, the generators, the console, PLang.Tests/Shared, the project files).
affected_suites() { # stdin = changed files
  local f s all=0; declare -A hit=()
  while read -r f; do
    [ -z "$f" ] && continue
    s=$(echo "$f" | sed -nE 's|^PLang\.Tests/([^/]+)/.*|\1|p')
    if [ -n "$s" ] && [[ " ${PROJECTS[*]} " == *" $s "* ]]; then hit[$s]=1; else all=1; fi
  done
  if [ "$all" = 1 ]; then echo "${PROJECTS[*]}"; else echo "${!hit[*]}"; fi
}

# Run every suite IN PARALLEL and report each one's result. Stdin is /dev/null, so no suite blocks
# on input; a suite the --timeout cap cuts off (partial counts — untested failures would vanish) is
# reported as CUT OFF, never as a count. Parallel totals matched a sequential run suite for suite
# (2026-09-28: 954/670/466/826/189/815); sweep 339s → 87s. A suite is RED if it reports
# `failed: N>0` or never prints a summary (the runner can segfault at teardown
# AFTER printing — intermittent — so pass/fail is read from the summary, not the
# exit code).
# Rough per-suite wall-clock baselines (sequential; parallel runs ~2-4× each under shared load,
# 2026-07-10, this machine, warm build). Noisy
# (machine load / JIT), so treat as a drift signal, not a gate: if actual ≫ expected
# consistently, a suite grew a slow test or a perf regression landed — investigate.
# Re-measured 2026-09-22 after the truncation fix: the old Modules=27 / Runtime=15 were
# taken while the 15s cap was cutting those suites off mid-run, so they timed a partial
# suite. Wire=25 is its first real measurement — before the snapshot write door was typed
# it stack-overflowed and never reached a summary.
declare -A SUITE_SECS=( [Generator]=4 [Types]=8 [Runtime]=24 [Wire]=25 [Modules]=43 [Data]=28 )

run_all_suites() { # $1 = configuration, $2 = the suites to run (space-separated; empty = all), rest = extra args
  local config="$1" p fail=0 running=0; local -a suites
  read -r -a suites <<< "${2:-${PROJECTS[*]}}"; shift 2
  # Each suite's FULL output is written to a per-suite log — read those for the truth
  # (the live stdout below is only the one-line summary and can be truncated under a pipe).
  echo "→ full per-suite output: /tmp/devsh_<Suite>.log  (Suite ∈ ${suites[*]})"
  echo "→ expected suite times (s, drift signal): $(for p in "${suites[@]}"; do printf '%s~%s ' "$p" "${SUITE_SECS[$p]:-?}"; done)"
  # At most DEVSH_PARALLEL at once (stdin is /dev/null, so no suite blocks on input); DEVSH_SEQUENTIAL=1
  # runs one at a time, to compare total: counts — a suite cut off by --timeout under load reports fewer.
  if [ -z "${DEVSH_SEQUENTIAL:-}" ]; then
    for p in "${suites[@]}"; do
      run_bin "$config" "$p" "$@" > "/tmp/devsh_$p.log" 2>&1 &
      running=$((running + 1))
      if [ "$running" -ge "$DEVSH_PARALLEL" ]; then wait -n || true; running=$((running - 1)); fi
    done
    wait || true
  else
    for p in "${suites[@]}"; do run_bin "$config" "$p" "$@" > "/tmp/devsh_$p.log" 2>&1 || true; done
  fi
  for p in "${suites[@]}"; do
    local n dur
    # Anchored to the SUMMARY block's own lines ("  failed: 44"), never a substring of
    # assertion text — a message reading 'Data failed: ...' was being read as the count.
    # `|| true` on both: a suite that dies before printing a summary makes these greps
    # return 1, and under `set -e` that killed the WHOLE sweep at that suite (Wire), so
    # every later suite silently never ran and the NO SUMMARY branch below was dead code.
    n=$(grep -aE '^[[:space:]]*failed: [0-9]+[[:space:]]*$' "/tmp/devsh_$p.log" | tail -1 | grep -oE '[0-9]+' || true)
    dur=$(grep -aoE 'duration: [0-9smh ]+' "/tmp/devsh_$p.log" | tail -1 | sed 's/duration: //' || true)
    local tag="${dur:-?} vs ~${SUITE_SECS[$p]:-?}s" line
    if grep -aq "Canceling the test session" "/tmp/devsh_$p.log"; then
      line="=== $p === CUT OFF by --timeout $TEST_TIMEOUT — counts are partial — see /tmp/devsh_$p.log [$tag]"; fail=1; SWEEP_COMPLETE=0
    elif [ -z "$n" ]; then line="=== $p === NO SUMMARY (crash before summary?) — see /tmp/devsh_$p.log [$tag]"; fail=1; SWEEP_COMPLETE=0
    elif [ "$n" != 0 ]; then line="=== $p === FAILED: $n ($(grep -aoE 'total: [0-9]+' /tmp/devsh_$p.log | tail -1)) [$tag]"; fail=1
    else line="=== $p === green ($(grep -aoE 'total: [0-9]+' /tmp/devsh_$p.log | tail -1)) [$tag]"; fi
    echo "$line"
    # a complete result is what an unchanged sweep answers with next time; a cut-off one never replaces it
    [[ "$line" == *"CUT OFF"* || "$line" == *"NO SUMMARY"* ]] || echo "$line" > "$SWEEP.line.$p"
  done
  return $fail
}
SWEEP_COMPLETE=1

# Run a build; on ANY compile error, SCREAM (impossible to miss) and hard-STOP before
# running tests — a non-compiling project leaves a STALE artefact, so any result run
# against it is a LIE (the stale-binary trap). This applies to EVERY build, not just the
# test projects: a stale PLang.dll (library) or plang.exe (console) poisons results just
# as badly. Compile errors are NOT test failures; fix them first.
#   $1 = human label (what's building), $2 = log path, rest = the build command
scream_build() {
  local label="$1" log="$2"; shift 2
  local rc errs
  "$@" > "$log" 2>&1 && rc=0 || rc=$?
  # Match CS compile errors from ANY project path (PLang/, PlangConsole/, PLang.Tests/…).
  # `|| true`: on a CLEAN build grep matches nothing → returns 1, and pipefail+set -e
  # would kill the script on success. We WANT empty errs there, not an abort.
  errs=$(grep -aoE '[^ ]+\.cs\([0-9]+,[0-9]+\): error [A-Z0-9]+[^[]*' "$log" | sort -u || true)
  { [ "$rc" = 0 ] && [ -z "$errs" ]; } && return 0
  echo
  echo "########################################################################"
  echo "##                                                                    ##"
  echo "##   🛑🛑🛑  BUILD FAILED — COMPILE ERRORS, NOT TEST FAILURES  🛑🛑🛑   ##"
  echo "##   A non-compiling project runs a STALE artefact — results are LIES. ##"
  echo "##   FIX COMPILATION FIRST. Do not read any test output below.        ##"
  echo "##                                                                    ##"
  echo "########################################################################"
  echo "##   what failed to build:  $label"
  echo
  if [ -n "$errs" ]; then echo "$errs"
  else echo "  (rc=$rc, no CS error lines matched — raw log tail:)"; tail -25 "$log"; fi
  echo
  echo "  (full build log: $log)"
  echo "########################################################################"
  exit 2
}
# Skip a build when no source changed since the last successful one. The stamp is touched only
# after a clean build (scream_build exits on failure first), so a failed build is retried next time.
# Directories count too (-newer on a dir = an entry added or deleted), so a deleted file is a change.
STAMP=.devsh-stamp
unchanged() { # $1 = the stamp of what was built
  [ -f "$1" ] && [ -z "$(find PLang PLang.Generators PlangConsole PLang.Tests \
    -path '*/bin' -prune -o -path '*/obj' -prune -o -newer "$1" -print -quit)" ]
}
# Builds and runs yield to the shared machine (Ingi's terminals, other bots). A compiler server
# (MSBuild node, VBCSCompiler) started by a niced build is niced too; one already running keeps its
# priority until `dotnet build-server shutdown`.
NICE=(nice -n 10)
# Test projects AND the console in one MSBuild evaluation (PLang.Tests/All.proj holds both) — the
# console is a dependency of every plang test; a stale plang.exe lies just like a stale dll.
# Nothing is built when no source changed. A whole build is also every test project's own build.
build_all() {
  local started=$SECONDS p
  if unchanged "$STAMP"; then echo "→ no source changed since the last build — skipping it"; return 0; fi
  scream_build "test projects + PlangConsole (+PLang library)" /tmp/devsh_build.log \
    "${NICE[@]}" dotnet msbuild PLang.Tests/All.proj -t:Build "${DEVFLAGS[@]}"
  touch "$STAMP"; for p in "${PROJECTS[@]}"; do touch "$STAMP.$p"; done
  echo "→ build: $((SECONDS - started))s"
}
# One test project (and what it references: PLang, Shared) — a class filter runs one project, so it
# builds only that one. Same global properties as All.proj passes, so the incremental state is shared.
build_project() { # $1 = the suite
  local started=$SECONDS
  if unchanged "$STAMP.$1"; then echo "→ no source changed since $1 was built — skipping it"; return 0; fi
  scream_build "PLang.Tests.$1 (+PLang library)" /tmp/devsh_build.log \
    "${NICE[@]}" dotnet msbuild "PLang.Tests/$1/PLang.Tests.$1.csproj" -t:Build "${DEVFLAGS[@]}"
  touch "$STAMP.$1"
  echo "→ build $1: $((SECONDS - started))s"
}

case "${1:-build}" in
  build)
    build_all
    ;;
  suite)
    # ONE whole suite by name — the middle ground between a class filter (too narrow to show a
    # blast radius) and the full sweep (six suites, ~3min). Use this to answer "is this failure
    # mine?": run the suite, stash, run it again, diff the failing NAMES.
    build_all
    p="${2:-}"
    case " ${PROJECTS[*]} " in
      *" $p "*) ;;
      *) echo "usage: ./dev.sh suite {${PROJECTS[*]}}" >&2; exit 2 ;;
    esac
    echo "=== $p ===  (full output: /tmp/devsh_$p.log)"
    run_bin Debug "$p" > "/tmp/devsh_$p.log" 2>&1 || true
    grep -aiE '^failed |^  (total|failed):' "/tmp/devsh_$p.log" || echo "  (no failures — full output in the log)"
    ;;
  test)
    if [ -n "${2:-}" ]; then
      # find the project whose sources mention the class (Shared holds helpers, not tests); fall back to all.
      # Only those projects build.
      hits=$(grep -rl "class ${2}" PLang.Tests/*/ --include=*.cs 2>/dev/null | grep -v /obj/ | sed 's|PLang.Tests/||;s|/.*||' \
        | grep -xF "$(printf '%s\n' "${PROJECTS[@]}")" | sort -u || true)
      if [ -z "$hits" ]; then hits="${PROJECTS[*]}"; build_all; else for p in $hits; do build_project "$p"; done; fi
      for p in $hits; do
        echo "=== $p ===  (full output: /tmp/devsh_$p.log)"
        started=$SECONDS
        run_bin Debug "$p" --treenode-filter "/*/*/*${2}*/*" > "/tmp/devsh_$p.log" 2>&1 || true
        grep -aiE '^failed |^  (total|failed):' "/tmp/devsh_$p.log" || echo "  (no failures — full output in the log)"
        echo "→ run $p: $((SECONDS - started))s"
      done
    else
      build_all
      # a sweep: only what changed since the last complete one (--force runs every suite again)
      if [ "${2:-}" != "--force" ] && [ -f "$SWEEP" ]; then
        changed=$(changed_since_sweep)
        if [ -z "$changed" ]; then
          echo "→ no C# change since the last sweep — its results stand (./dev.sh test --force runs again):"
          for p in "${PROJECTS[@]}"; do cat "$SWEEP.line.$p" 2>/dev/null || echo "=== $p === (no stored result)"; done
          [ -n "$(git status --porcelain -- '*.goal')" ] && echo "→ .goal files changed — the plang tests are ./dev.sh ptest"
          exit 0
        fi
        suites=$(echo "$changed" | affected_suites)
      else
        suites="${PROJECTS[*]}"
      fi
      for p in "${PROJECTS[@]}"; do
        [[ " $suites " == *" $p "* ]] || echo "$(cat "$SWEEP.line.$p" 2>/dev/null || echo "=== $p ===") (unchanged since the last sweep — not run)"
      done
      rc=0; run_all_suites Debug "$suites" || rc=$?
      # only a complete sweep is the next one's reference (a suite cut off or crashed is run again)
      [ "$SWEEP_COMPLETE" = 1 ] && snapshot > "$SWEEP"
      exit $rc
    fi
    ;;
  ptest)
    build_all
    (cd test && ../PlangConsole/bin/Debug/net10.0/plang --test)
    ;;
  full)
    echo "→ 'full' runs ALL suites + plang tests (slow, pre-commit gate)."
    echo "  For a faster loop: ./dev.sh test <ClassName>  (one suite/class, ~seconds)."
    echo "  Per-suite full output is written to /tmp/devsh_<Suite>.log — read those, not this stdout."
    # Pre-commit gate: analyzers ON, in its own configuration (Gate → bin/Gate, obj/Gate), so it
    # never invalidates the analyzers-off Debug incremental state. PLang.csproj turns analyzers off
    # only for Debug, so under Gate they run on the library too (PLNG001/PLNG002).
    # Still routed through scream_build so a compile error screams and hard-stops.
    scream_build "test projects + PlangConsole (Gate, analyzers ON)" /tmp/devsh_build.log \
      nice -n 10 dotnet msbuild PLang.Tests/All.proj -t:Build -p:Configuration=Gate -p:RunAnalyzers=true -v:q -nologo
    fail=0
    run_all_suites Gate "" || fail=1
    (cd test && nice -n 10 ../PlangConsole/bin/Gate/net10.0/plang --test) || fail=1
    exit $fail
    ;;
  warm)
    dotnet msbuild PLang.Tests/All.proj -t:Build "${DEVFLAGS[@]}" >/dev/null 2>&1 || true
    echo warm
    ;;
  *)
    echo "usage: ./dev.sh {build|test [ClassFilter]|suite <Suite>|ptest|full|warm}" >&2; exit 2
    ;;
esac
