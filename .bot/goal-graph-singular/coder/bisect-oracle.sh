#!/usr/bin/env bash
# Oracle for the security-shaped Wire regressions on goal-graph-singular.
#
#   exit 0   = all named tests pass   (commit is GOOD)
#   exit 1   = any named test fails   (commit is BAD)
#   exit 125 = cannot judge           (build broke, or a test does not exist yet) → git bisect skips
#
# Usage:
#   ./bisect-oracle.sh                      # default test set (the tamper pair)
#   TESTS="A B" ./bisect-oracle.sh          # explicit set
#   git bisect run .bot/<branch>/coder/bisect-oracle.sh
#
# WHY THIS IS NOT TRIVIAL — both of these fail SILENTLY as "skip" if unhandled:
#
#  1. The test layout CHANGES across this branch: one PLang.Tests project at the base, six suites
#     (Modules/Types/Wire/Data/Generator/Runtime) later. The oracle detects which exists.
#  2. Build artefacts do NOT survive a bisect checkout. A stale obj/ from the other layout makes
#     msbuild emit nothing while reporting success. Cleaning PLang/bin alone is worse than useless:
#     the test projects reference its OUTPUT and fail with "could not copy PLangLibrary.dll" rather
#     than rebuilding it. Hence the full wipe below (the CLAUDE.md stale-binary recipe) and the
#     explicit library build before any test project.
#
# Blind `git bisect run` over the whole 1700-commit branch is unreliable for reasons of pace, not
# correctness — prefer narrowing with `git log <base>..HEAD -- <paths>` and testing named candidates
# against their parents. This script is the gap-closer for small ranges.

set -uo pipefail
# PLANG_ROOT lets this run against a worktree (narrow-then-test checks a candidate and its parent
# without disturbing the main tree). Defaults to the main checkout for `git bisect run`.
cd "${PLANG_ROOT:-/workspace/plang}" || exit 125

read -r -a TEST_LIST <<< "${TESTS:-Cut4_TamperingPropertyValue_FailsOuterSignatureVerify OuterSignature_AfterPropertiesValueTamper_FailsVerify}"

# --- full wipe: the documented stale-binary recipe ------------------------------------------
rm -rf PlangConsole/bin PlangConsole/obj \
       PLang/bin PLang/obj \
       PLang.Tests/bin PLang.Tests/obj \
       PLang.Generators/bin PLang.Generators/obj
for p in Modules Types Wire Data Generator Runtime Shared; do
  rm -rf "PLang.Tests/$p/bin" "PLang.Tests/$p/obj"
done

# --- the library first: test projects reference its output, they do not rebuild it -----------
dotnet build PLang/PLang.csproj -c Debug -v q --nologo > /tmp/oracle_lib.log 2>&1 || exit 125

# --- build whichever test layout this commit has ---------------------------------------------
BINS=()
if [ -f PLang.Tests/PLang.Tests.csproj ]; then
  dotnet build PLang.Tests/PLang.Tests.csproj -c Debug -v q --nologo > /tmp/oracle_build.log 2>&1 || exit 125
  BINS=(PLang.Tests/bin/Debug/net10.0/PLang.Tests)
else
  for p in Modules Types Wire Data Generator Runtime; do
    [ -f "PLang.Tests/$p/PLang.Tests.$p.csproj" ] || continue
    dotnet build "PLang.Tests/$p/PLang.Tests.$p.csproj" -c Debug -v q --nologo >> /tmp/oracle_build.log 2>&1 || exit 125
    BINS+=("PLang.Tests/$p/bin/Debug/net10.0/PLang.Tests.$p")
  done
  [ ${#BINS[@]} -eq 0 ] && exit 125
fi

# --- run each named test against whichever binary carries it ---------------------------------
verdict=0
for t in "${TEST_LIST[@]}"; do
  found=0
  for bin in "${BINS[@]}"; do
    [ -x "$bin" ] || continue
    out=$("$bin" --timeout 120s --treenode-filter "/*/*/*/*${t}*" < /dev/null 2>&1)
    total=$(printf '%s' "$out" | grep -aoE 'total: [0-9]+' | tail -1 | grep -oE '[0-9]+')
    [ -z "${total:-}" ] && continue
    [ "$total" -eq 0 ] && continue
    found=1
    failed=$(printf '%s' "$out" | grep -aoE 'failed: [0-9]+' | tail -1 | grep -oE '[0-9]+')
    [ "${failed:-1}" -ne 0 ] && verdict=1
    break
  done
  # A test that does not exist at this commit makes the commit unjudgeable, never "good".
  [ "$found" -eq 0 ] && exit 125
done

exit $verdict
