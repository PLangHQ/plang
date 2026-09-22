#!/usr/bin/env bash
# Blocks shell commands that would change production source invisibly.
#
# Production C# and .goal files must be changed through the Edit/Write tools so every
# change renders in the console. sed/perl/python/awk/tee and redirection do not render.
#
# Denies when the command both (a) uses a writer tool or redirection and (b) names a
# production target. Reads (cat, grep, git, dotnet) are untouched.

set -uo pipefail

cmd=$(jq -r '.tool_input.command // ""')
[ -z "$cmd" ] && exit 0

# Test C# may be shell-batched — it is not production and Ingi does not review it line by line.
# Drop whole PLang.Tests path tokens before checking, so a command naming only those has no
# production target left and passes; one that also names production still trips the rules below.
# Flag tokens (--include=*.cs) are filters, never write targets, so they drop too.
cmd=$(printf '%s' "$cmd" | tr ' \t' '\n\n' | grep -v 'PLang\.Tests/' | grep -v '^-' | tr '\n' ' ')

# A production target: a .cs or .goal file, or any path under a production project.
TARGET='(PLang/|PLang\.Generators/|PlangConsole/|(^|[[:space:]"'"'"'/])os/|\.cs([[:space:]"'"'"';)|&]|$)|\.goal([[:space:]"'"'"';)|&]|$))'

# A writer tool anywhere in the command.
WRITER='(^|[[:space:];&|(])(sed|perl|awk|python|python3|tee)([[:space:]]|$)'

# Redirection whose target is a production file.
REDIRECT='>>?[[:space:]]*"?'"'"'?[^[:space:]"'"'"';|&]*(\.cs|\.goal)'

deny() {
  jq -n --arg r "$1" '{hookSpecificOutput:{hookEventName:"PreToolUse",permissionDecision:"deny",permissionDecisionReason:$r}}'
  exit 0
}

if [[ "$cmd" =~ $WRITER ]] && [[ "$cmd" =~ $TARGET ]]; then
  deny "Blocked: sed/perl/python/awk/tee against production source. Ingi must see every change to production C# and .goal files in the console — use the Edit or Write tool, one file at a time. Never sed a rename across many files."
fi

if [[ "$cmd" =~ $REDIRECT ]]; then
  deny "Blocked: shell redirection into a .cs or .goal file. Use the Edit or Write tool so the change is visible in the console."
fi

exit 0
