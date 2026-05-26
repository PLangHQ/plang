#!/usr/bin/env bash
# Drift checker for Documentation/v0.2/app-tree.md.
#
# Compares the doc against the source under PLang/app/ and reports:
#   - module folders that exist in source but not in the doc (and vice versa)
#   - public properties on app.@this not mentioned in the doc
#   - public properties on actor.@this not mentioned in the doc
#   - data/this.*.cs partials not mentioned in the doc
#
# This does NOT rewrite the doc. The narrative (annotations, "What's NOT on
# app", casing convention) stays hand-curated. The script only catches the
# mechanical omissions — the part that actually rots.
#
# Exit codes: 0 if clean, 1 if any drift found.

set -euo pipefail
export LC_ALL=C.UTF-8 LANG=C.UTF-8

repo_root="$(cd "$(dirname "$0")/../../.." && pwd)"
doc="$repo_root/Documentation/v0.2/app-tree.md"
app="$repo_root/PLang/app"

if [[ ! -f "$doc" ]]; then echo "missing $doc" >&2; exit 2; fi
if [[ ! -d "$app" ]]; then echo "missing $app" >&2; exit 2; fi

drift=0
note() { echo "DRIFT: $*"; drift=1; }

# Whole-word match against the doc.
in_doc() { grep -qE "(^|[^A-Za-z0-9_])$1([^A-Za-z0-9_]|$)" "$doc"; }

# 1. modules/<name>/ folders ↔ "modules/" tree block
# Skip PascalCase infrastructure that lives under modules/ but isn't a
# registered action vocabulary (Schema is the LLM action catalog object
# owned by Modules — documented separately at the foot of the modules
# section, not as a bullet in the action list).
module_skip_regex='^(Schema)$'
mapfile -t module_dirs < <(find "$app/modules" -mindepth 1 -maxdepth 1 -type d -printf '%f\n' | sort)
for m in "${module_dirs[@]}"; do
  [[ "$m" =~ $module_skip_regex ]] && continue
  # Lines in the modules block look like:  ├── name   — ...   or   └── name   — ...
  if ! grep -qE "^[├└]── $m( |$)" "$doc"; then
    note "module '$m' exists under PLang/app/modules/ but no '── $m' line in app-tree.md"
  fi
done

# Reverse: bullet lines in the modules block that don't have a matching folder.
while IFS= read -r line; do
  name="$(sed -E 's/^[├└]── ([A-Za-z_]+).*/\1/' <<<"$line")"
  [[ -z "$name" ]] && continue
  # Only check names that look like module candidates (skip top-level tree lines).
  if [[ ! -d "$app/modules/$name" ]] && [[ -d "$app/modules" ]]; then
    # Only flag if it appears inside the modules block. Crude heuristic: line
    # is between the "app.Modules" header and the next fenced close.
    :
  fi
done < <(awk '/^app\.Modules/{flag=1;next} flag && /^```/{flag=0} flag' "$doc")

# Proper reverse pass: extract names from the modules fenced block.
mapfile -t doc_modules < <(
  awk '/^app\.Modules/{flag=1;next} flag && /^```/{flag=0;exit} flag' "$doc" \
    | sed -nE 's/^[├└]── ([A-Za-z_]+).*/\1/p'
)
for n in "${doc_modules[@]}"; do
  if [[ ! -d "$app/modules/$n" ]]; then
    note "app-tree.md lists module '$n' but no folder PLang/app/modules/$n/"
  fi
done

# 2. Public properties on app.@this
mapfile -t app_props < <(
  grep -oE '^\s*public\s+[^=(){};]+\s+([A-Z][A-Za-z0-9_]*)\s*(\{|=>)' "$app/this.cs" \
    | sed -E 's/.*\s([A-Z][A-Za-z0-9_]*)\s*(\{|=>).*/\1/' \
    | sort -u
)
# Names that are deliberately not surfaced in the one-screen doc.
app_skip_regex='^(OsAbsolutePath)$'
for p in "${app_props[@]}"; do
  [[ "$p" =~ $app_skip_regex ]] && continue
  in_doc "$p" || note "app.@this property '$p' not mentioned in app-tree.md"
done

# 3. Public properties on actor.@this
mapfile -t actor_props < <(
  grep -oE '^\s*public\s+[^=(){};]+\s+([A-Z][A-Za-z0-9_]*)\s*(\{|=>)' "$app/actor/this.cs" \
    | sed -E 's/.*\s([A-Z][A-Za-z0-9_]*)\s*(\{|=>).*/\1/' \
    | sort -u
)
actor_skip_regex='^(FoundationalChannels)$'
for p in "${actor_props[@]}"; do
  [[ "$p" =~ $actor_skip_regex ]] && continue
  in_doc "$p" || note "actor.@this property '$p' not mentioned in app-tree.md"
done

# 4. Data partials
mapfile -t data_partials < <(
  find "$app/data" -maxdepth 1 -name 'this.*.cs' -printf '%f\n' \
    | sed -E 's/^this\.(.+)\.cs$/\1/' | sort -u
)
for d in "${data_partials[@]}"; do
  in_doc "$d" || note "data partial 'this.$d.cs' not mentioned in app-tree.md"
done

if (( drift == 0 )); then
  echo "app-tree.md: clean (${#module_dirs[@]} modules, ${#app_props[@]} app props, ${#actor_props[@]} actor props, ${#data_partials[@]} data partials)"
  exit 0
else
  echo "" >&2
  echo "Update Documentation/v0.2/app-tree.md to resolve the drift above." >&2
  exit 1
fi
