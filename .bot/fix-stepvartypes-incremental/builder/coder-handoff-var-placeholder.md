# Coder handoff: stop hardcoding `%var% string` in the action catalog

**From:** builder
**Branch:** `fix-stepvartypes-incremental`
**Severity:** low — currently masked by a Compile.llm prompt patch, but the prompt patch is a workaround. Permanent fix is in C#.

## The bug

`PLang/app/modules/this.cs:320`:

```csharp
// Variable slots advertise as "%var% string" so the LLM emits
// a variable name (with or without %), not the literal type token.
var desc = hasVar ? "%var% string" : typeName;
```

Every `data.@this<Variable>` parameter — i.e. every action that takes a variable *name* (write target, read-by-name, list operations, etc.) — renders into the action catalog as `<ParamName>: %var% string`. The trailing `string` is hardcoded and unconditional. It tells the LLM the variable holds a string.

That's a lie. `data.@this<Variable>` only constrains the slot to *name a variable*; what that variable resolves to at runtime is unconstrained (could be a list, dict, bool, object — anything).

## The user-visible symptom

Compile-time warning the LLM raises on perfectly correct compiles:

```
list.any expects ListName as a string variable name, but the step provides
%results% (typed as object in scope). Emitting it as given.
```

Reproduced on `Tests/Modules/Test/Run/TestRunReportsAssertionFailure.test.goal` step 4:

```
- list.any %results% where Status equals 'Fail', write to %hasFail%
```

`%results%` is a list (from a prior `test.run`); the schema says `ListName: %var% string`; the variables-in-scope snapshot says `%results%(object)`; LLM flags an `ambiguousMapping` warning because the two declared types don't match. The compile output itself is correct — the warning is noise.

Once one prompt teaches the LLM to ignore this discrepancy (see "Current workaround" below) the warnings stop, but the catalog is still lying. Any future LLM that hasn't internalised the workaround will produce the same warning, or worse, "correct" the variable by wrapping it in something stringy.

## Current workaround (already shipped on this branch)

`os/system/builder/llm/Compile.llm` — added a paragraph next to the existing `%var%` explanation telling the LLM that the trailing token after `%var%` is a placeholder, not a type claim. Commit lands in the same push as this handoff.

That's a teaching patch. It works but it documents around a catalog-rendering inaccuracy instead of fixing it.

## What "fixed" looks like

Pick one of these (no strong preference — coder picks based on what the LLM behaves best on; structurally they're equivalent):

**Option 1: drop the trailing type entirely.**
```csharp
var desc = hasVar ? "%var%" : typeName;
```
Catalog reads `- ListName: %var%`. Clean. The `%var%` marker alone tells the LLM "this slot takes a variable reference; the variable can hold anything."

**Option 2: render the placeholder as `any`.**
```csharp
var desc = hasVar ? "%var% any" : typeName;
```
Catalog reads `- ListName: %var% any`. Marginally more informative: the `any` makes it explicit the variable's resolved type is unconstrained.

**Option 3: when the C# slot has a real generic constraint, honour it.** Today the property is always `data.@this<Variable>` so there is no constraint to read. If you ever introduce a typed variable slot (e.g. `data.@this<Variable<string>>`), render `%var% string` then — but ONLY then. As long as `Variable` has no generic parameter, fall back to Option 1 or 2.

I'd lean Option 1: the `%var%` token alone is already documented in `Compile.llm` as "this slot takes a variable reference", and adding any trailing token only invites the same confusion in a different costume.

## Verification

After fixing, on this same branch:

```bash
cd /workspace/plang/Tests
rm -rf Modules/Test/Run/.build/testrunreportsassertionfailure.test.pr
/workspace/plang/PlangConsole/bin/Debug/net10.0/plang build \
  '--build={"files":"Modules/Test/Run/TestRunReportsAssertionFailure.test.goal","cache":false}'
```

Inspect the latest trace under `.build/traces/*/TestRunReportsAssertionFailure.json`. Step 4's response should have:
- `formal: list.any(ListName=%results%, ...)` — unchanged
- `warnings: []` — unchanged from the post-prompt-patch state
- The user prompt's `## Action Detail` section should now show `ListName: %var%` (or `%var% any`) instead of `%var% string`.

Once the C# fix lands, the corresponding paragraph in `Compile.llm` (the one about "the trailing token after `%var%` is a generic placeholder, **not** a type claim") can be deleted — its only reason to exist is the hardcoded `string` it's working around.

## Out of scope

- Don't change `Variable`'s shape. The `IRawNameResolvable` contract (CLAUDE.md PLNG001 paragraph) is fine as-is; this fix is purely about how `data.@this<Variable>` parameters are *described* to the LLM, not how they resolve at runtime.
- Don't touch `os/system/modules/<m>/<action>.notes.md` files in scope of this fix. They don't reference the placeholder.
