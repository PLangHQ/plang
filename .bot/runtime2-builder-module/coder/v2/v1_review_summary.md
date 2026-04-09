# v1 Review Summary

Ingi reviewed the builder module code and raised 8 issues:

1. **Goal.MergeFrom duplicate step text** — `FirstOrDefault(s => s.Text == step.Text)` fails when two steps in one goal have identical text. Both would match the first existing step, losing the second's actions.

2. **GoalFile hash uses inline SHA256** — Should use a module.action (e.g., crypto.hash) instead of raw `SHA256.HashData`. Rule: if a module.action exists, use it.

3. **File I/O uses engine.FileSystem directly** — Must use `engine.RunAction<file.Read>()` and `file.List` actions. This allows the file module to be overridden (goals stored somewhere other than filesystem).

4. **System goals filtered out** — Should mark `IsSystem = true` instead of excluding. Let the caller decide.

5. **JsonException silently swallowed** — Corrupt .pr files should return the error to the user, not hide it.

6. **JsonSerializerOptions duplicated** — `new JsonSerializerOptions { PropertyNameCaseInsensitive = true }` defined in multiple places. Need shared static instances.

7. **Action naming uses verbs** — OBP rule: actions should be nouns. `builder.actions` not `builder.getActions`, `builder.app` not `builder.getApp`, etc.

8. **validateActions scans all .build folders** — Overkill. Paths are relative to the goal being built. `/somefile.txt` = root, `somefile.txt` = goal's directory.

9. **saveGoals omits nulls** — .pr files must be deterministic. Save all properties including nulls so runtime default changes don't alter behavior.
