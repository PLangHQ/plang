# Action-Based Conditions — Design Plan

## The Problem

The current `condition.if` handler has a single `bool Condition` parameter:

```csharp
public partial bool Condition { get; init; }
```

The builder puts expressions like `%x% > 5` into this parameter as a string with type "bool". At runtime, the lazy resolver interpolates variables (`%x%` → `10`), producing `"10 > 5"`. Then it calls `TypeMapping.ConvertTo("10 > 5", typeof(bool))` → `Convert.ChangeType` → **crash**. There's no expression evaluator.

This means the `ConditionCompound` tests (which have expressions like `%x% > 5`, `%name% contains "world"`) can't actually work at runtime. The .pr files exist, but execution would fail.

Runtime1 solved this with `ConditionEvaluator` — a structured `Condition` record with `LeftValue`, `Operator`, `RightValue`, and recursive `CompoundCondition` for AND/OR. That works, but it's a monolithic approach where the condition module needs to know about every possible check (file exists, string contains, db count, etc.).

## The Insight

App already supports **multiple actions per step**. Actions within a step execute in order, and a later action can reference the return variable of an earlier action. This is the composition mechanism.

Instead of building a Swiss Army knife condition module, we decompose conditions into what they really are:

1. **A value-producing action** (optional) — e.g., `file.exists` returns a bool
2. **A comparison** — the condition module compares two values
3. **A branch** — call GoalIfTrue/GoalIfFalse, or execute indented sub-steps

The condition module only does step 2 and 3. Any module can participate in step 1 without the condition module knowing about it.

## The Design

### Two Actions: `condition.if` and `condition.compare`

**`condition.if`** — evaluates AND branches. Always either calls a goal or controls indented sub-steps. Never "just returns a bool."

**`condition.compare`** — pure evaluation. Returns a bool. Used as an intermediate in compound conditions (AND/OR) where sub-results feed into the final `if`.

### `condition.if` Parameters

```csharp
[Action("if")]
public partial class If : IContext
{
    public partial object? Left { get; init; }        // value or %variable%
    public partial string? Operator { get; init; }    // null = truthy check
    public partial object? Right { get; init; }       // null for unary ops
    public partial GoalCall? GoalIfTrue { get; init; }
    public partial GoalCall? GoalIfFalse { get; init; }
}
```

The `if` handler always branches:
- **Goal mode:** GoalIfTrue/GoalIfFalse are set → call the appropriate goal
- **Sub-step mode:** No goals set → return the bool result, and the step runner uses it to control indented children

### `condition.compare` Parameters

```csharp
[Action("compare")]
public partial class Compare : IContext
{
    public partial object? Left { get; init; }
    public partial string Operator { get; init; }   // required for compare
    public partial object? Right { get; init; }

    public Task<Data> Run()
    {
        var evaluator = ResolveEvaluator();
        bool result = evaluator.Evaluate(Left, Operator, Right);
        return Task.FromResult(Data.Ok(result));
    }
}
```

### Evaluation Rules

| Left | Operator | Right | Behavior |
|------|----------|-------|----------|
| `%flag%` | null | null | Truthy check: `IsTruthy(Left)` |
| `%x%` | `>` | `5` | Compare: `Left > Right` |
| `%name%` | `contains` | `"world"` | String op: `Left.Contains(Right)` |
| `%list%` | `isEmpty` | null | Unary: `IsEmpty(Left)` |
| `%a%` | `NOT` | null | Unary: `!IsTruthy(Left)` |
| `%c1%` | `AND` | `%c2%` | Logical: `Left && Right` |

### Supported Operators

**Comparison:** `==`, `!=`, `<`, `>`, `<=`, `>=`
**String:** `contains`, `startswith`, `endswith`
**Collection:** `in`, `isEmpty`
**Logical:** `AND`, `OR`
**Unary:** `NOT`

---

## Sub-Step Execution (Indented Blocks)

### The Constraint

Step objects are **readonly and shared**. They are loaded once from .pr files and cached. Multiple concurrent requests (web server) use the same Step instances. You cannot put runtime state (`Execute = true/false`) on Step — that's a race condition.

### The Design

Indented steps default to **not executing**. They must be "proven true" by a parent condition. The execution decision is made entirely by the step runner (`Steps.RunAsync`) using **local per-invocation state** — no mutation of Step, no mutation of shared context.

```plang
- if %x% > 5
    - write out "x is big"        ← indent 4, only runs if condition is true
    - set %result% = "big"        ← indent 4, only runs if condition is true
- write out "always runs"          ← indent 0, always runs
```

### Steps.RunAsync — Sub-Step Logic

```csharp
// Steps.RunAsync — local state only, fully thread-safe
int? skipBelowIndent = null;

foreach (var step in this)
{
    // Indented child of a false condition — skip
    if (skipBelowIndent != null)
    {
        if (step.Indent > skipBelowIndent)
            continue;
        skipBelowIndent = null; // back to parent level, resume
    }

    var result = await step.RunAsync(engine, context, ct);
    if (!result.Success) return result;

    // Bool false + indented children below = skip the block
    if (result.Value is false && HasIndentedChildren(step))
    {
        skipBelowIndent = step.Indent;
    }

    merged = merged.Merge(result);
}

private bool HasIndentedChildren(Step.@this step)
{
    var idx = IndexOf(step);
    return idx + 1 < Count && this[idx + 1].Indent > step.Indent;
}
```

Key properties:
- **Thread-safe:** `skipBelowIndent` is a local variable — each concurrent request gets its own
- **Step is never mutated:** The decision is purely in the runner's loop
- **OBP-compliant:** Steps owns the iteration logic (collections own their loops)
- **Convention-based:** A step returning `bool false` with indented children triggers skipping. The condition handler doesn't know about sub-steps — it just returns a bool

### Nested Conditions

```plang
- if %x% > 5
    - if %y% < 10
        - write out "both true"       ← indent 8
    - write out "x > 5 only"          ← indent 4
- write out "always"                   ← indent 0
```

When `if %y% < 10` returns false, `skipBelowIndent = 4` (the inner if's indent). Steps with indent > 4 are skipped (the doubly-indented "both true"). "x > 5 only" is at indent 4 — not greater, so `skipBelowIndent` resets and it executes. This handles arbitrary nesting correctly.

### Build-Time Validation

The builder must enforce: **indented steps may only appear under a condition step.** If someone writes:

```plang
- set %x% = 5
    - write out "this makes no sense"
```

This is a structural error. The validator checks: if a step's indent > the previous step's indent, the previous step must map to `condition.if`. This is a deterministic check — no LLM involved. It belongs in structural validation (the builder consistency framework).

---

## Pluggable Comparison Engine (Providers)

### The Pattern

The comparison engine is pluggable. The default ships with PLang, but users can swap it:

```plang
- set comparison engine as mycomparer.dll
```

This pattern applies generically across modules — db (SQLite/Postgres), template (Mustache/Razor), crypto (providers), etc. Each module owns its provider interface and default implementation.

### Module-Local Providers

Providers live **in the module they belong to**:

```
modules/
  condition/
    if.cs
    compare.cs
    providers/
      IEvaluator.cs          — interface
      DefaultEvaluator.cs    — ships with PLang
  db/
    providers/
      IDbProvider.cs
      SqliteProvider.cs       — default
  template/
    providers/
      IRenderer.cs
      DefaultRenderer.cs      — default
```

### IEvaluator Interface

```csharp
namespace App.modules.condition.providers;

public interface IEvaluator
{
    bool Evaluate(object? left, string op, object? right);
    bool IsTruthy(object? value);
}
```

### DefaultEvaluator

Ported from runtime1's `ConditionEvaluator`, adapted for runtime2:

```csharp
namespace App.modules.condition.providers;

public class DefaultEvaluator : IEvaluator
{
    public bool Evaluate(object? left, string op, object? right)
    {
        (left, right) = NormalizeTypes(left, right);

        return op.ToLowerInvariant() switch
        {
            "==" => Equals(left, right),
            "!=" => !Equals(left, right),
            ">" => Compare(left, right) > 0,
            "<" => Compare(left, right) < 0,
            ">=" => Compare(left, right) >= 0,
            "<=" => Compare(left, right) <= 0,
            "contains" => Contains(left, right),
            "startswith" => StringOp(left, right,
                (s, r) => s.StartsWith(r, StringComparison.OrdinalIgnoreCase)),
            "endswith" => StringOp(left, right,
                (s, r) => s.EndsWith(r, StringComparison.OrdinalIgnoreCase)),
            "in" => In(left, right),
            "isempty" => IsEmpty(left),
            "not" => !IsTruthy(left),
            "and" => IsTruthy(left) && IsTruthy(right),
            "or" => IsTruthy(left) || IsTruthy(right),
            _ => throw new NotSupportedException($"Operator '{op}'")
        };
    }

    public bool IsTruthy(object? value) => value switch
    {
        null => false,
        bool b => b,
        int i => i != 0,
        long l => l != 0,
        double d => d != 0.0,
        decimal m => m != 0m,
        string s => !string.IsNullOrWhiteSpace(s),
        ICollection c => c.Count > 0,
        _ => true
    };

    // ... NormalizeTypes, Compare, Contains, StringOp, In, IsEmpty helpers
}
```

### Provider Resolution

The condition handler resolves its evaluator through `engine.Libraries`:

```csharp
// In If.Run()
var evaluator = Context.Engine!.Libraries.GetProvider<IEvaluator>()
    ?? new DefaultEvaluator();
```

`GetProvider<T>()` is a new method on Libraries that scans registered libraries for an implementation of interface `T`. First external match wins, fallback to default. This is the generic mechanism — every module uses the same `GetProvider<T>()` call with its own interface.

A user swaps the provider via:
```plang
- use library 'mycomparer.dll'
```
The DLL contains a class implementing `IEvaluator`. Libraries discovers it on load. Next time `GetProvider<IEvaluator>()` is called, it returns the custom implementation.

### Type Normalization

When comparing Left and Right, types must match. JSON deserialization boxes numbers as int/long/double inconsistently. Before comparing:

1. Both numeric → convert to the wider type (int → long → double → decimal)
2. One string, one numeric → try converting string to number
3. String comparison is case-insensitive by default

This reuses the pattern from runtime1's `TypeHelper.TryConvertToMatchingType`.

---

## How Each PLang Pattern Maps to .pr

**1. Simple boolean variable** — `if %flag%, call DoThing`

Single action, no operator (truthy check):
```json
{
  "actions": [{
    "module": "condition", "action": "if",
    "parameters": [
      {"name": "Left", "value": "%flag%", "type": "object"},
      {"name": "GoalIfTrue", "value": "DoThing", "type": "goal.call"}
    ]
  }]
}
```

**2. Value comparison** — `if %x% > 5, call WhenGreater`

Single action with operator:
```json
{
  "actions": [{
    "module": "condition", "action": "if",
    "parameters": [
      {"name": "Left", "value": "%x%", "type": "object"},
      {"name": "Operator", "value": ">", "type": "string"},
      {"name": "Right", "value": 5, "type": "int"},
      {"name": "GoalIfTrue", "value": "WhenGreater", "type": "goal.call"}
    ]
  }]
}
```

**3. Sub-step mode** — `if %x% > 5` with indented children

No goals — the `if` returns a bool, step runner controls children:
```json
{
  "actions": [{
    "module": "condition", "action": "if",
    "parameters": [
      {"name": "Left", "value": "%x%", "type": "object"},
      {"name": "Operator", "value": ">", "type": "string"},
      {"name": "Right", "value": 5, "type": "int"}
    ]
  }]
}
```
The next steps in the .pr have `"indent": 4` (or higher). Steps.RunAsync skips them if result is false.

**4. Action-based condition** — `if file.txt exists, call ProcessFile`

Multi-action step — file.exists runs first, condition.if reads its result:
```json
{
  "actions": [
    {
      "module": "file", "action": "exists",
      "parameters": [{"name": "Path", "value": "file.txt", "type": "string"}],
      "return": [{"name": "%__fileExists%"}]
    },
    {
      "module": "condition", "action": "if",
      "parameters": [
        {"name": "Left", "value": "%__fileExists%", "type": "object"},
        {"name": "GoalIfTrue", "value": "ProcessFile", "type": "goal.call"}
      ]
    }
  ]
}
```

**5. Action + comparison** — `if select count(*) from users > 0, call HasUsers`

```json
{
  "actions": [
    {
      "module": "db", "action": "scalar",
      "parameters": [{"name": "sql", "value": "select count(*) from users", "type": "string"}],
      "return": [{"name": "%__userCount%"}]
    },
    {
      "module": "condition", "action": "if",
      "parameters": [
        {"name": "Left", "value": "%__userCount%", "type": "object"},
        {"name": "Operator", "value": ">", "type": "string"},
        {"name": "Right", "value": 0, "type": "int"},
        {"name": "GoalIfTrue", "value": "HasUsers", "type": "goal.call"}
      ]
    }
  ]
}
```

**6. Compound AND/OR** — `if %x% > 5 and %y% < 10, call DoThing`

Each sub-condition is a `compare` action, final `if` combines with AND:
```json
{
  "actions": [
    {
      "module": "condition", "action": "compare",
      "parameters": [
        {"name": "Left", "value": "%x%", "type": "object"},
        {"name": "Operator", "value": ">", "type": "string"},
        {"name": "Right", "value": 5, "type": "int"}
      ],
      "return": [{"name": "%__cond1%"}]
    },
    {
      "module": "condition", "action": "compare",
      "parameters": [
        {"name": "Left", "value": "%y%", "type": "object"},
        {"name": "Operator", "value": "<", "type": "string"},
        {"name": "Right", "value": 10, "type": "int"}
      ],
      "return": [{"name": "%__cond2%"}]
    },
    {
      "module": "condition", "action": "if",
      "parameters": [
        {"name": "Left", "value": "%__cond1%", "type": "object"},
        {"name": "Operator", "value": "AND", "type": "string"},
        {"name": "Right", "value": "%__cond2%", "type": "object"},
        {"name": "GoalIfTrue", "value": "DoThing", "type": "goal.call"}
      ]
    }
  ]
}
```

**7. Sub-steps with action** — `if file.txt exists` with indented children

```json
{
  "actions": [
    {
      "module": "file", "action": "exists",
      "parameters": [{"name": "Path", "value": "file.txt", "type": "string"}],
      "return": [{"name": "%__fileExists%"}]
    },
    {
      "module": "condition", "action": "if",
      "parameters": [
        {"name": "Left", "value": "%__fileExists%", "type": "object"}
      ]
    }
  ]
}
```
No goals, no operator — truthy check on the file.exists result. Sub-steps controlled by the runner.

---

## What Changes

### Files to Create

| File | Purpose |
|------|---------|
| `PLang/App/modules/condition/compare.cs` | Pure evaluation action (returns bool, no branching) |
| `PLang/App/modules/condition/providers/IEvaluator.cs` | Interface for pluggable comparison engine |
| `PLang/App/modules/condition/providers/DefaultEvaluator.cs` | Default comparison engine (ported from runtime1) |

### Files to Modify

| File | Change |
|------|--------|
| `PLang/App/modules/condition/if.cs` | Replace `bool Condition` with `Left`/`Operator`/`Right`. Use IEvaluator. Two modes: goal call + sub-step. |
| `PLang/App/Goals/Goal/Steps/this.cs` | Add sub-step skip logic to `RunAsync`. Add `HasIndentedChildren` helper. |
| `PLang/App/Goals/Goal/Steps/Step/Actions/this.cs` | No change needed — multi-action already works. |
| `system/builder/llm/BuildGoal.llm` | Update condition examples to show Left/Operator/Right format. Add multi-action condition example. Add sub-step example. Add rule: never pack expressions into a single bool parameter. |
| `PLang/App/Utility/GoalMapper.cs` | Map new condition parameters (if GoalMapper does condition-specific mapping). |

### Files That Don't Change

- `Step/Methods.cs` — step execution unchanged
- `Action/Methods.cs` — action execution unchanged
- `LazyParamsGenerator.cs` — handles `object?` and `string?` types already
- `TypeMapping.cs` — no new types needed

### Builder Prompt Changes

In `BuildGoal.llm`, update the condition section:

```
## Condition Rules

- `condition.if` uses Left/Operator/Right parameters — NEVER pack expressions into a single bool parameter
- When Operator is omitted, Left is treated as a truthy check
- `condition.if` ALWAYS branches: either with GoalIfTrue/GoalIfFalse goals, or by controlling indented sub-steps
- `condition.compare` returns a bool for intermediate results — use it in compound conditions (AND/OR)
- For action-based conditions (file exists, db query, etc.), use multi-action steps: the producing action first, then condition.if
- Indented steps (indent > 0) are sub-steps of the preceding condition — they only execute when the condition is true

Examples:
- if %count%, call ProcessData
  → condition.if: Left=%count%, GoalIfTrue=ProcessData

- if %x% > 5, call WhenGreater
  → condition.if: Left=%x%, Operator=">", Right=5, GoalIfTrue=WhenGreater

- if %flag%\n    - write out "flag is set"
  → condition.if: Left=%flag% (no goals — sub-step mode, next steps have indent: 4)

- if file.txt exists, call ProcessFile
  → file.exists(Path="file.txt", return=%__exists%) + condition.if(Left=%__exists%, GoalIfTrue=ProcessFile)

- if %x% > 5 and %y% < 10, call DoThing
  → condition.compare(Left=%x%, Operator=">", Right=5, return=%__c1%)
  + condition.compare(Left=%y%, Operator="<", Right=10, return=%__c2%)
  + condition.if(Left=%__c1%, Operator="AND", Right=%__c2%, GoalIfTrue=DoThing)
```

### .pr Format — No Backward Compatibility

Per CLAUDE.md: "No backward compatibility on .pr file format changes." All existing condition .pr files must be rebuilt. The `Condition` parameter name is replaced by `Left`/`Operator`/`Right`.

---

## OBP Compliance

- **Behavior on the owner:** Evaluation logic lives in the module's `providers/` folder. Handlers navigate to it. Steps owns the iteration and sub-step skip logic.
- **Collections own their loops:** Steps.RunAsync owns the skip-indented-children logic — not Step, not the condition handler.
- **Step is immutable and shared:** No runtime state on Step. The skip decision is local to the runner's invocation.
- **Navigate, don't pass:** Handlers access the evaluator through `engine.Libraries.GetProvider<IEvaluator>()`. The evaluator receives Left/Operator/Right — it navigates the values.
- **No parsing:** The LLM produces structured Left/Operator/Right in the .pr file. The runtime never parses expression strings.
- **Providers are module-local:** Each module owns its interface and default. No global provider registry.

---

## Libraries.GetProvider<T> — Generic Provider Resolution

New method on Libraries to support the pluggable provider pattern across all modules:

```csharp
// On Libraries (engine.Libraries)
public T? GetProvider<T>() where T : class
{
    // Walk libraries (external first, then built-in)
    // Find first type implementing T
    // Return instance (cached per interface)
}
```

This is the single generic mechanism. Every module calls `engine.Libraries.GetProvider<IWhatever>()` with its own interface. External libraries loaded via `use library 'x.dll'` are scanned for implementations. First match wins.

---

## Test Estimation

### C# Unit Tests (~20)

**DefaultEvaluator (~14):**
- Each operator: ==, !=, <, >, <=, >=, contains, startswith, endswith, in, isEmpty, NOT, AND, OR
- Type normalization: int vs long, string vs number, null comparisons
- IsTruthy: null→false, bool, int 0→false, empty string→false, empty list→false, non-empty→true

**If handler (~3):**
- Truthy check (no operator), comparison (with operator), goal branching (true/false paths)

**Compare handler (~1):**
- Returns bool, no branching

**Steps sub-step logic (~4):**
- False condition skips indented children
- True condition executes indented children
- Nested conditions (inner false, outer true)
- No indented children — no skip even on false

### PLang Tests (~14)

- Simple boolean true: `if %flag%` → executes GoalIfTrue
- Simple boolean false: `if %noFlag%` → executes GoalIfFalse
- Greater than: `if %x% > 5, call WhenGreater`
- Less than: `if %y% < 10, call WhenLess`
- Equals: `if %z% == 5, call WhenEqual`
- Not equals: `if %a% != 3, call WhenNotEqual`
- Contains: `if %name% contains "world", call WhenContains`
- Else branch: `if %x% > 100, call A, else call B`
- Sub-steps true: `if %flag%` with indented steps → they execute
- Sub-steps false: `if %noFlag%` with indented steps → they're skipped
- Nested sub-steps: inner and outer conditions
- Compound AND: `if %a% > 1 and %b% < 10, call DoThing`
- Compound OR: `if %a% == 1 or %b% == 2, call DoThing`
- Negation: `if not %flag%, call WhenFalse`
