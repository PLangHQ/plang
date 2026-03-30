# Auditor v2 Plan — Fix Verification + Fresh Eyes

## Scope
1. Verify coder fixes for all 3 v1 auditor findings
2. Fresh-eyes review of the full branch — specifically the Parse() changes and their cross-file implications

## What Changed
Single commit `0f2aae39`: 4 production changes + 3 new tests:
- `Goal/this.cs:270` — simplified `/` comment check (removed `//` exclusion)
- `Goal/this.cs:328-342` — new `\` escape continuation feature
- `Methods.cs:108,144` — `JsonSerializerOptions.Default` (was `new`)
- 3 new tests: `ExcludesProviderProperties`, `DoubleSlash`, `BackslashEscape`

## Fresh Eyes Focus
- **ToText() ↔ Parse() round-trip**: Parse now has `\` escapes and `//` comments. Does ToText produce output that Parse can consume?
- **FormatForLlm template**: Does the template handle multiline step text from `\` escapes correctly?
- **Builder prompt**: Does BuildGoal.llm know about the `\` syntax?
- **Existing .goal files**: Do any system/builder .goal files use `//` or start with `/` in step text?
