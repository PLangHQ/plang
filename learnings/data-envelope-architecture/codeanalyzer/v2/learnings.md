# Learnings — data-envelope-architecture / Code Analyzer v2

**Source:** Reviewing coder v5 + tester v7 with fresh eyes, applying learnings from runtime2-settings review

## 1. JSON number unwrapping is a type-surface problem, not just int/long

`UnwrapJsonElement` handles `TryGetInt64 → GetDouble`. But JSON carries three implicit numeric types: integer, floating-point, and decimal. `19.99` as double is `19.989999999999998...`. Same pattern as the Settings int→long issue: the code handles one type transition but misses others in the same family. Always ask: "what are ALL the types that flow through this generic conversion?"

## 2. Clone methods are a family — audit them together

`MemoryStack.Clone()` doesn't propagate `_context`. Same lesson as `PLangContext.Clone()` not copying `SettingsScope` on the other branch. When reviewing a property addition to any object, check ALL methods that create copies of that object: constructors, Clone, CreateChild, factory methods, deserialization paths.

## 3. Return type contract changes need integration tests

`GetChild` changed from returning `null` to returning `Data.FromError(...)` on depth exceeded. This changes the contract for ALL callers — `MemoryStack.Get` being the primary one. The unit test proves GetChild works correctly in isolation. No test proves callers handle the new return type correctly. Same lesson as the simulation vs integration test on runtime2-settings.

## 4. Generic exception catches mask specific error types

`fromJson.Run()` catches `Exception` and wraps everything as "JsonParseError". When a depth limit was added to `UnwrapJsonElement`, the new `InvalidOperationException` gets caught by the same generic handler with a wrong error key. Lesson: when adding a new throw site, trace ALL catch sites that might intercept it and verify they produce the right error identity.

## 5. Rehydration heuristics are fragile across user data

`RehydrateNestedData` detects Data-shaped dictionaries by checking for a "value" key. This works for the compression pipeline (which only processes Data-shaped JSON), but user data containing `{"name": "price", "value": 19.99}` would be falsely rehydrated after a compress→decompress cycle. Heuristics that depend on key names are fragile when user data can have the same keys.

## 6. Seven rounds of review still finding new things

The data-envelope-architecture branch went through 7 tester rounds and 5 coder rounds. Each round fixed the previous findings but introduced new code paths that needed review. The lesson: security hardening (depth limits, cycle detection) adds code that itself needs testing. Every fix is a new surface to analyze. Reviews should focus disproportionately on new code from the previous fix round.
