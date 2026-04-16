# v1 Summary: Full Analysis of runtime2-builder-plan

## What this is

Full 5-pass code analysis of a massive branch (~200 changed files) implementing Data<T> composition, return removal, condition orchestration, foreach inline body, builder eval suite, and new modules (timer, list.any, list.group).

## What was done

Analyzed all changed runtime C# files across core (Data, Variables, Goal, Step, Action, App), modules (condition, loop, builder, list, timer, error), source generator, and utilities. Found 23 findings total, 3 critical.

## Critical Findings

1. **Foreach dictionary support silently dropped** (`loop/foreach.cs`): Old version yielded (key, value) pairs for dictionaries. New version uses `AsEnumerable()` which yields raw `KeyValuePair<,>` structs. `%key%` changed from dictionary key to numeric loop index. Silent breaking change.

2. **ResolveDeep mutates shared CLR object properties** (`Variables/this.cs`): New typed-object resolution writes resolved values back to the original object via `prop.SetValue()`. If that object is shared across executions (e.g., Action.Parameters from .pr data), the mutation contaminates the template for subsequent executions.

3. **Old .pr files silently lose Return mappings** (`Action/this.cs`): `Return` changed from `[Store]` to `[JsonIgnore]`. Old .pr files with `"return"` arrays will have them silently ignored during deserialization. No migration or version check.

## Should-Fix Findings (8)

- Data.Value getter not stable across reads (NeedsResolution creates new collections)
- Implicit operator creates Data<T> with empty Name
- PromoteGroups.SetValue is no-op for JsonElement
- list.any/group don't handle POCO objects (only dict/JsonElement)
- As<T>() uses uncached reflection
- Console.ReadLine in build guard hangs headless/CI
- Handled flag broadened from error-only to general flow control
- Condition orchestration guard may block nested goals (needs verification)

## Verdict: FAIL

Suggest sending back to coder for fixes on the 3 critical issues. The architectural direction (Data<T> composition, return removal, inline bodies) is excellent OBP. The issues are in the migration edges and the behavioral corners.
