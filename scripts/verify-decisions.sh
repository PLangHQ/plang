#!/usr/bin/env bash
# verify-decisions.sh — run every executable `check:` in decisions.md.
# Exit 0 if all decisions hold; non-zero if any diverged.
# A `check:` is a shell command run from the repo root; exit 0 = decision holds.
#
# Usage: verify-decisions.sh [path-to-decisions.md]
set -uo pipefail

FILE="${1:-Documentation/v0.2/decisions.md}"

if [ ! -f "$FILE" ]; then
    # No decisions file yet = nothing to guard. Not an error.
    echo "Decision Guard: no decisions file at '$FILE' — nothing to verify."
    exit 0
fi

fail=0
id="(unnamed)"
count=0

echo "Decision Guard ($FILE):"
while IFS= read -r line || [ -n "$line" ]; do
    case "$line" in
        "## "*)
            id="${line#'## '}"
            ;;
        check:*)
            cmd="${line#check:}"
            # trim leading whitespace
            cmd="${cmd#"${cmd%%[![:space:]]*}"}"
            count=$((count + 1))
            if bash -c "$cmd" >/dev/null 2>&1; then
                printf "  %-48s PASS\n" "$id"
            else
                printf "  %-48s FAIL\n" "$id"
                printf "      check: %s\n" "$cmd"
                fail=1
            fi
            ;;
    esac
done < "$FILE"

if [ "$count" -eq 0 ]; then
    echo "  (no check: lines found)"
fi

if [ "$fail" -ne 0 ]; then
    echo ""
    echo "decision-guard: a design decision DIVERGED (see FAIL above)."
    echo "Fix the code, OR — if the decision genuinely changed — update decisions.md WITH Ingi's sign-off."
fi

exit $fail
