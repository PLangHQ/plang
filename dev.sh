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

run_bin() { # $1 = project, rest = args
  "PLang.Tests/$1/bin/Debug/net10.0/PLang.Tests.$1" "${@:2}"
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
      fail=0
      for p in "${PROJECTS[@]}"; do
        echo "=== $p ==="
        run_bin "$p" || fail=1
      done
      exit $fail
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
    for p in "${PROJECTS[@]}"; do
      echo "=== $p ==="
      run_bin "$p" || fail=1
    done
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
