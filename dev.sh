#!/usr/bin/env bash
# Fast inner-loop build/test for PLang development.
#
# Usage:
#   ./dev.sh build              # incremental build of all test projects + PlangConsole (analyzers off); skipped when no source changed
#   ./dev.sh test [filter]      # build, then run C# tests; filter = test-class name (finds the right project), e.g. ./dev.sh test ReturnTests; no filter = all suites in parallel
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
# (Modules) takes ~85s under shared load (2026-09-28) — keep the cap well above that.
# Override via TEST_TIMEOUT=Ns.
TEST_TIMEOUT="${TEST_TIMEOUT:-240s}"

run_bin() { # $1 = configuration (Debug | Gate), $2 = project, rest = args
  # < /dev/null: a test that reads stdin (a stream/ask-channel test) otherwise BLOCKS
  # waiting for input until the whole-suite --timeout cap fires — turning a few-second
  # run into a multi-minute hang. EOF on stdin lets it fail fast instead.
  "PLang.Tests/$2/bin/$1/net10.0/PLang.Tests.$2" --timeout "$TEST_TIMEOUT" "${@:3}" < /dev/null
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

run_all_suites() { # $1 = configuration, rest = extra args passed to each suite
  local config="$1" p fail=0; shift
  # Each suite's FULL output is written to a per-suite log — read those for the truth
  # (the live stdout below is only the one-line summary and can be truncated under a pipe).
  echo "→ full per-suite output: /tmp/devsh_<Suite>.log  (Suite ∈ ${PROJECTS[*]})"
  echo "→ expected suite times (s, drift signal): $(for p in "${PROJECTS[@]}"; do printf '%s~%s ' "$p" "${SUITE_SECS[$p]:-?}"; done)"
  # Parallel by default (stdin is /dev/null, so no suite blocks on input); DEVSH_SEQUENTIAL=1 runs one
  # at a time, to compare total: counts — a suite cut off by --timeout under load reports fewer.
  if [ -z "${DEVSH_SEQUENTIAL:-}" ]; then
    for p in "${PROJECTS[@]}"; do run_bin "$config" "$p" "$@" > "/tmp/devsh_$p.log" 2>&1 & done
    wait || true
  else
    for p in "${PROJECTS[@]}"; do run_bin "$config" "$p" "$@" > "/tmp/devsh_$p.log" 2>&1 || true; done
  fi
  for p in "${PROJECTS[@]}"; do
    local n dur
    # Anchored to the SUMMARY block's own lines ("  failed: 44"), never a substring of
    # assertion text — a message reading 'Data failed: ...' was being read as the count.
    # `|| true` on both: a suite that dies before printing a summary makes these greps
    # return 1, and under `set -e` that killed the WHOLE sweep at that suite (Wire), so
    # every later suite silently never ran and the NO SUMMARY branch below was dead code.
    n=$(grep -aE '^[[:space:]]*failed: [0-9]+[[:space:]]*$' "/tmp/devsh_$p.log" | tail -1 | grep -oE '[0-9]+' || true)
    dur=$(grep -aoE 'duration: [0-9smh ]+' "/tmp/devsh_$p.log" | tail -1 | sed 's/duration: //' || true)
    local tag="${dur:-?} vs ~${SUITE_SECS[$p]:-?}s"
    if grep -aq "Canceling the test session" "/tmp/devsh_$p.log"; then
      echo "=== $p === CUT OFF by --timeout $TEST_TIMEOUT — counts are partial — see /tmp/devsh_$p.log [$tag]"; fail=1
    elif [ -z "$n" ]; then echo "=== $p === NO SUMMARY (crash before summary?) — see /tmp/devsh_$p.log [$tag]"; fail=1
    elif [ "$n" != 0 ]; then echo "=== $p === FAILED: $n ($(grep -aoE 'total: [0-9]+' /tmp/devsh_$p.log | tail -1)) [$tag]"; fail=1
    else echo "=== $p === green ($(grep -aoE 'total: [0-9]+' /tmp/devsh_$p.log | tail -1)) [$tag]"; fi
  done
  return $fail
}

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
unchanged() {
  [ -f "$STAMP" ] && [ -z "$(find PLang PLang.Generators PlangConsole PLang.Tests \
    -path '*/bin' -prune -o -path '*/obj' -prune -o -newer "$STAMP" -print -quit)" ]
}
# Test projects AND the console in one MSBuild evaluation (PLang.Tests/All.proj holds both) — the
# console is a dependency of every plang test; a stale plang.exe lies just like a stale dll.
# Nothing is built when no source changed.
build_all() {
  if unchanged; then echo "→ no source changed since the last build — skipping it"; return 0; fi
  scream_build "test projects + PlangConsole (+PLang library)" /tmp/devsh_build.log \
    dotnet msbuild PLang.Tests/All.proj -t:Build "${DEVFLAGS[@]}"
  touch "$STAMP"
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
    build_all
    if [ -n "${2:-}" ]; then
      # find the project whose sources mention the class; fall back to all
      hits=$(grep -rl "class ${2}" PLang.Tests/*/ --include=*.cs 2>/dev/null | grep -v /obj/ | sed 's|PLang.Tests/||;s|/.*||' | sort -u)
      [ -z "$hits" ] && hits="${PROJECTS[*]}"
      for p in $hits; do
        echo "=== $p ===  (full output: /tmp/devsh_$p.log)"
        run_bin Debug "$p" --treenode-filter "/*/*/*${2}*/*" > "/tmp/devsh_$p.log" 2>&1 || true
        grep -aiE '^failed |^  (total|failed):' "/tmp/devsh_$p.log" || echo "  (no failures — full output in the log)"
      done
    else
      run_all_suites Debug
      exit $?
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
      dotnet msbuild PLang.Tests/All.proj -t:Build -p:Configuration=Gate -p:RunAnalyzers=true -v:q -nologo
    fail=0
    run_all_suites Gate || fail=1
    (cd test && ../PlangConsole/bin/Gate/net10.0/plang --test) || fail=1
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
