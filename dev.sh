#!/usr/bin/env bash
# Fast inner-loop build/test for PLang development.
#
# Usage:
#   ./dev.sh build              # incremental build of PLang.Tests + PlangConsole (analyzers off)
#   ./dev.sh test [filter]      # build, then run C# tests; optional TUnit class filter, e.g. ./dev.sh test ReturnTests
#   ./dev.sh ptest              # build console, then run plang tests (from Tests/)
#   ./dev.sh full               # the pre-commit build: analyzers ON (PLNG001/PLNG002 gates, TUnit warnings) + both suites
#   ./dev.sh warm               # background-friendly warmup; run once at session start (absorbs the after-idle stall)
#
# Why the flags matter (measured 2026-06-10, see .bot/compare-redesign/coder/build-speed-report.md):
#   - dotnet run --project PLang.Tests  → 90s+ per call (restore + MSBuild eval + build + run). Never use it.
#   - -p:RunAnalyzers=false             → test-project compile 47s → 31s; PLang-only edit 13s → 4.6s.
#     MUST be used consistently: alternating the flag invalidates incremental state (full rebuild).
#     Source generators still run (tests stay correct); only diagnostics are skipped — `full` restores them.
#   - The compiled test binary is run directly; TUnit filter syntax: --treenode-filter "/*/*/<ClassName>/*"
set -euo pipefail
cd "$(dirname "$0")"
export DOTNET_CLI_USE_MSBUILD_SERVER=1

DEVFLAGS=(-c Debug --no-restore -p:RunAnalyzers=false -v q --nologo)
TESTBIN=PLang.Tests/bin/Debug/net10.0/PLang.Tests

case "${1:-build}" in
  build)
    time dotnet build PLang.Tests "${DEVFLAGS[@]}"
    time dotnet build PlangConsole "${DEVFLAGS[@]}"
    ;;
  test)
    dotnet build PLang.Tests "${DEVFLAGS[@]}"
    if [ -n "${2:-}" ]; then
      "$TESTBIN" --treenode-filter "/*/*/*${2}*/*"
    else
      "$TESTBIN"
    fi
    ;;
  ptest)
    dotnet build PlangConsole "${DEVFLAGS[@]}"
    (cd Tests && ../PlangConsole/bin/Debug/net10.0/plang --test)
    ;;
  full)
    # Pre-commit gate: analyzers ON. This build is slower AND invalidates the
    # analyzers-off incremental state once — run it when handing off, not per edit.
    dotnet build PLang.Tests -c Debug --no-restore -v q --nologo
    dotnet build PlangConsole -c Debug --no-restore -v q --nologo
    "$TESTBIN"
    (cd Tests && ../PlangConsole/bin/Debug/net10.0/plang --test)
    ;;
  warm)
    dotnet build PLang.Tests "${DEVFLAGS[@]}" >/dev/null 2>&1 || true
    dotnet build PlangConsole "${DEVFLAGS[@]}" >/dev/null 2>&1 || true
    echo warm
    ;;
  *)
    echo "usage: ./dev.sh {build|test [ClassFilter]|ptest|full|warm}" >&2; exit 2
    ;;
esac
