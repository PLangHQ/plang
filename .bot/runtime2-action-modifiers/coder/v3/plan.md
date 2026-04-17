# Coder v3 Plan — Tests for Data.IsVariable, HasVariableReference, and variable.set.ValidateBuild

## Context

Tester v2 found 3 must-fix items: new code on this branch at 0% test coverage.

## Tasks

### 1. Add `Data.IsVariable` tests to DataTests.cs

Edge cases per tester:
- `%var%` → true (standard variable)
- `%v%` → true (short name)
- `%%` → false (empty, length ≤ 2)
- `hello %var%` → false (not entire value)
- `%var% + 1` → false (not entire value)
- non-string value (42) → false
- null value → false

### 2. Add `Data.HasVariableReference` tests to DataTests.cs

Edge cases per tester:
- `hello %name%` → true
- `%a% + %b%` → true
- `%var%` → true (single variable also matches)
- `no vars` → false
- `%%` → false (regex `%[^%]+%` requires at least one char between)
- non-string value (42) → false
- null value → false

### 3. Add `variable.set.ValidateBuild()` tests to SetTests.cs

Three paths:
1. Value is literal `"this"` → returns error string containing "this"
2. Value has `%variable%` reference → returns null (skip validation)
3. Type mismatch (type=int, value="not a number") → returns error string
4. Valid type match (type=int, value=42) → returns null

## Approach

- Tests go in existing files (DataTests.cs, SetTests.cs)
- `ValidateBuild` is static, takes `List<Data.@this>` — call directly, no need for App.Run
- Follow existing TUnit patterns in these files
