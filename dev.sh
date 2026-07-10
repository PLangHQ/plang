#!/usr/bin/env bash
# Fast inner-loop build/test for PLang development.
#
# Usage:
#   ./dev.sh build              # incremental build of all test projects + PlangConsole (analyzers off)
#   ./dev.sh test [filter]      # build, then run C# tests; filter = test-class name (finds the right project), e.g. ./dev.sh test ReturnTests
#   ./dev.sh ptest              # build console, then run plang tests (from Tests/)
#   ./dev.sh full               # the pre-commit gate: analyzers ON (PLNG001/PLNG002, TUnit warnings) + ALL suites
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
# was the 5-minute "Wire hang". The cap cancels it so the suite finishes. Sized just
# above the suites' real completion time (~5-12s); too low (e.g. 5s) truncates a big
# suite mid-run. Once the stdin-blocking test is fixed (see todos.md), this net can
# drop or go away. Override via TEST_TIMEOUT=Ns.
TEST_TIMEOUT="${TEST_TIMEOUT:-15s}"

run_bin() { # $1 = project, rest = args
  # < /dev/null: a test that reads stdin (a stream/ask-channel test) otherwise BLOCKS
  # waiting for input until the whole-suite --timeout cap fires — turning a few-second
  # run into a multi-minute hang. EOF on stdin lets it fail fast instead.
  "PLang.Tests/$1/bin/Debug/net10.0/PLang.Tests.$1" --timeout "$TEST_TIMEOUT" "${@:2}" < /dev/null
}

# Run every suite SEQUENTIALLY (one at a time) and report each one's result.
# NOT parallel: the whole-suite --timeout cap is a wall-clock budget, so under
# parallel CPU contention a big suite runs slower and gets TRUNCATED mid-run —
# untested failures silently vanish (measured: Modules ran 34 of 1004). One at a
# time, each suite gets full CPU, finishes well inside the cap, and reports true
# counts. (Once the stdin-hang test is fixed, the cap goes away and these can run
# parallel with no truncation — see todos.md.) A suite is RED if it reports
# `failed: N>0` or never prints a summary (the runner can segfault at teardown
# AFTER printing — intermittent — so pass/fail is read from the summary, not the
# exit code).
# Rough per-suite wall-clock baselines (2026-07-10, this machine, warm build). Noisy
# (machine load / JIT), so treat as a drift signal, not a gate: if actual ≫ expected
# consistently, a suite grew a slow test or a perf regression landed — investigate.
declare -A SUITE_SECS=( [Generator]=6 [Types]=8 [Runtime]=15 [Wire]=16 [Modules]=27 [Data]=35 )

run_all_suites() { # rest = extra args passed to each suite
  local p fail=0
  # Each suite's FULL output is written to a per-suite log — read those for the truth
  # (the live stdout below is only the one-line summary and can be truncated under a pipe).
  echo "→ full per-suite output: /tmp/devsh_<Suite>.log  (Suite ∈ ${PROJECTS[*]})"
  echo "→ expected suite times (s, drift signal): $(for p in "${PROJECTS[@]}"; do printf '%s~%s ' "$p" "${SUITE_SECS[$p]:-?}"; done)"
  for p in "${PROJECTS[@]}"; do
    run_bin "$p" "$@" > "/tmp/devsh_$p.log" 2>&1 || true
    local n dur
    n=$(grep -aoE 'failed: [0-9]+' "/tmp/devsh_$p.log" | tail -1 | grep -oE '[0-9]+')
    dur=$(grep -aoE 'duration: [0-9smh ]+' "/tmp/devsh_$p.log" | tail -1 | sed 's/duration: //')
    local tag="${dur:-?} vs ~${SUITE_SECS[$p]:-?}s"
    if [ -z "$n" ]; then echo "=== $p === NO SUMMARY (crash before summary?) — see /tmp/devsh_$p.log [$tag]"; fail=1
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
# The console is a dependency of every plang test; a stale plang.exe lies just like a stale dll.
build_console() { scream_build "PlangConsole (+PLang library)" /tmp/devsh_console.log \
  dotnet build PlangConsole -c Debug --no-restore -p:RunAnalyzers=false -v q --nologo; }
build_tests() { scream_build "test projects (PLang.Tests/*)" /tmp/devsh_build.log \
  dotnet msbuild PLang.Tests/All.proj -t:Build "${DEVFLAGS[@]}"; }

case "${1:-build}" in
  build)
    build_tests
    build_console
    ;;
  test)
    build_tests
    if [ -n "${2:-}" ]; then
      # find the project whose sources mention the class; fall back to all
      hits=$(grep -rl "class ${2}" PLang.Tests/*/ --include=*.cs 2>/dev/null | grep -v /obj/ | sed 's|PLang.Tests/||;s|/.*||' | sort -u)
      [ -z "$hits" ] && hits="${PROJECTS[*]}"
      for p in $hits; do
        echo "=== $p ===  (full output: /tmp/devsh_$p.log)"
        run_bin "$p" --treenode-filter "/*/*/*${2}*/*" > "/tmp/devsh_$p.log" 2>&1 || true
        grep -aiE '^failed |^  (total|failed):' "/tmp/devsh_$p.log" || echo "  (no failures — full output in the log)"
      done
    else
      run_all_suites
      exit $?
    fi
    ;;
  ptest)
    build_console
    (cd Tests && ../PlangConsole/bin/Debug/net10.0/plang --test)
    ;;
  full)
    echo "→ 'full' runs ALL suites + plang tests (slow, pre-commit gate)."
    echo "  For a faster loop: ./dev.sh test <ClassName>  (one suite/class, ~seconds)."
    echo "  Per-suite full output is written to /tmp/devsh_<Suite>.log — read those, not this stdout."
    # Pre-commit gate: analyzers ON. Slower, and invalidates the analyzers-off
    # incremental state once — run when handing off, not per edit.
    # Analyzers ON here (the gate) — so NOT build_tests/build_console (those are analyzers-off),
    # but still route through scream_build so a compile error screams and hard-stops.
    scream_build "test projects (analyzers ON)" /tmp/devsh_build.log \
      dotnet msbuild PLang.Tests/All.proj -t:Build -p:Configuration=Debug -v:q -nologo
    scream_build "PlangConsole (+PLang library, analyzers ON)" /tmp/devsh_console.log \
      dotnet build PlangConsole -c Debug --no-restore -v q --nologo
    fail=0
    run_all_suites || fail=1
    (cd Tests && ../PlangConsole/bin/Debug/net10.0/plang --test) || fail=1
    exit $fail
    ;;
  warm)
    dotnet msbuild PLang.Tests/All.proj -t:Build "${DEVFLAGS[@]}" >/dev/null 2>&1 || true
    dotnet build PlangConsole -c Debug --no-restore -p:RunAnalyzers=false -v q --nologo >/dev/null 2>&1 || true
    echo warm
    ;;
  *)
    echo "usage: ./dev.sh {build|test [ClassFilter]|ptest|full|warm}" >&2; exit 2
    ;;
esac
