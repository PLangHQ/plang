#!/usr/bin/env bash
# Binary search over an ordered candidate list (oldest first) in the $WT worktree.
# Assumes the commit before the first candidate is GOOD and HEAD is BAD. Logs every probe.
# Usage: bisect_cands.sh <candidates-file> <log-file>   (env: TESTS, REAL_SIGNING pass through)
set -uo pipefail
WT="${WT:?set WT to the worktree}"
CANDS="$(realpath "$1")"; LOG="$(realpath -m "$2")"   # absolute: probe() cds into the worktree
ORACLE=/workspace/plang/.bot/goal-graph-singular/coder/bisect-oracle.sh
mapfile -t C < "$CANDS"
lo=0; hi=$(( ${#C[@]} - 1 ))   # invariant: answer (first BAD) is in [lo, hi]
probe() {
  cd $WT && git checkout -q -- . && git checkout -q "$1" || return 125
  PLANG_ROOT=$WT bash "$ORACLE" > /dev/null 2>&1; local r=$?
  git -C $WT checkout -q -- .
  echo "$(date +%T) $1 -> $r" >> "$LOG"; return $r
}
while [ $lo -lt $hi ]; do
  mid=$(( (lo + hi) / 2 ))
  probe "${C[$mid]}"; r=$?
  if [ $r -eq 0 ]; then lo=$((mid + 1));
  elif [ $r -eq 1 ]; then hi=$mid;
  else
    # unjudgeable: try the next candidate up instead, shrinking toward hi
    echo "skip ${C[$mid]}" >> "$LOG"; C=("${C[@]:0:$mid}" "${C[@]:$((mid+1))}"); hi=$((hi - 1))
  fi
done
echo "FIRST BAD CANDIDATE: ${C[$lo]}" >> "$LOG"
p=$(git -C /workspace/plang rev-parse --short "${C[$lo]}^")
probe "$p"; echo "PARENT $p -> $?" >> "$LOG"
