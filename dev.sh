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
  "PLang.Tests/$1/bin/Debug/net10.0/PLang.Tests.$1" --timeout "$TEST_TIMEOUT" "${@:2}"
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
run_all_suites() { # rest = extra args passed to each suite
  local p fail=0
  for p in "${PROJECTS[@]}"; do
    run_bin "$p" "$@" > "/tmp/devsh_$p.log" 2>&1 || true
    local n
    n=$(grep -aoE 'failed: [0-9]+' "/tmp/devsh_$p.log" | tail -1 | grep -oE '[0-9]+')
    if [ -z "$n" ]; then echo "=== $p === NO SUMMARY (crash before summary?) — see /tmp/devsh_$p.log"; fail=1
    elif [ "$n" != 0 ]; then echo "=== $p === FAILED: $n ($(grep -aoE 'total: [0-9]+' /tmp/devsh_$p.log | tail -1))"; fail=1
    else echo "=== $p === green ($(grep -aoE 'total: [0-9]+' /tmp/devsh_$p.log | tail -1))"; fi
  done
  return $fail
}

case "${1:-build}" in
  build)
    time dotnet msbuild PLang.Tests/All.proj -t:Build "${DEVFLAGS[@]}"
    time dotnet build PlangConsole -c Debug --no-restore -p:RunAnalyzers=false -v q --nologo
    ;;
  test)
    dotnet msbuild PLang.Tests/All.proj -t:Build "${DEVFLAGS[@]}"
    if [ -n "${2:-}" ]; then
      # find the project whose sources mention the class; fall back to all
      hits=$(grep -rl "class ${2}" PLang.Tests/*/ --include=*.cs 2>/dev/null | grep -v /obj/ | sed 's|PLang.Tests/||;s|/.*||' | sort -u)
      [ -z "$hits" ] && hits="${PROJECTS[*]}"
      for p in $hits; do
        echo "=== $p ==="
        run_bin "$p" --treenode-filter "/*/*/*${2}*/*" || true
      done
    else
      run_all_suites
      exit $?
    fi
    ;;
  ptest)
    dotnet build PlangConsole -c Debug --no-restore -p:RunAnalyzers=false -v q --nologo
    (cd Tests && ../PlangConsole/bin/Debug/net10.0/plang --test)
    ;;
  full)
    # Pre-commit gate: analyzers ON. Slower, and invalidates the analyzers-off
    # incremental state once — run when handing off, not per edit.
    dotnet msbuild PLang.Tests/All.proj -t:Build -p:Configuration=Debug -v:q -nologo
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
