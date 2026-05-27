#!/usr/bin/env bash
# Run the PLang Semgrep rule set against the PLang library.
# See .semgrep/README.md for what each rule catches.
#
# Usage:
#   scripts/semgrep-scan.sh                # full PLang/ scan
#   scripts/semgrep-scan.sh path/to/file   # specific path(s)
#   scripts/semgrep-scan.sh --severity ERROR PLang/  # only ERROR-level
set -euo pipefail

REPO_ROOT="$(cd "$(dirname "$0")/.." && pwd)"
cd "$REPO_ROOT"

# pip --user installs land in ~/.local/bin, which isn't on PATH in all shells.
export PATH="$HOME/.local/bin:$PATH"

if ! command -v semgrep >/dev/null 2>&1; then
    echo "semgrep not installed. Install: pip install semgrep" >&2
    exit 127
fi

# Semgrep's default cache (~/.cache/semgrep_version) errors with "Permission
# denied" in some sandboxed harnesses. Redirect to a tmp dir.
if [ ! -w "${XDG_CACHE_HOME:-$HOME/.cache}" ] 2>/dev/null; then
    export XDG_CACHE_HOME="${XDG_CACHE_HOME:-/tmp/semgrep-cache}"
    export SEMGREP_SETTINGS_FILE="${SEMGREP_SETTINGS_FILE:-/tmp/semgrep-cache/settings.yml}"
    mkdir -p "$XDG_CACHE_HOME"
fi

# Quiet the version banner / opt out of telemetry by default.
export SEMGREP_ENABLE_VERSION_CHECK="${SEMGREP_ENABLE_VERSION_CHECK:-0}"

TARGETS=("$@")
if [ "${#TARGETS[@]}" -eq 0 ]; then
    TARGETS=("PLang/")
fi

exec semgrep --config .semgrep/ --metrics off "${TARGETS[@]}"
