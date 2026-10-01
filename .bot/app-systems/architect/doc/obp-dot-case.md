# Dot case: the text for the two OBP docs (architect's draft, for the coder to place)

## 1. `Documentation/v0.2/object_pattern_formal.md`, a new rule after "### The name is the contract" (before "### The three paths agree")

```markdown
### A name is a path

Never glue words. A compound name (`ListName`, `buildExecutionPath`, `MaxRedirects`) is a hierarchy written flat. Write it as a dot path, one word per segment: `setting.build.execution.path`, `redirect.max`.

Then check that the path navigates: each segment must be an owner in the code whose member is the next. When it navigates, the name sits where the thing lives. When it doesn't, the path shows where the thing really lives. `ListName` → `list.name` doesn't navigate: a list has no name; the variable holding it has one. So the name is `list.variable.name`, and the action takes `list`, the thing itself. `ListName` was a flat copy of `list.variable.name`.

This is the three paths applied to every single name, and it finds the same faults the smells name: a glued name that won't navigate is a *flat copy*, a *stray helper* or a *verb+noun*.

**Test**: write the name with dots. If a segment isn't an owner of the next, move the thing; don't rename it.
```

## 2. `Documentation/v0.2/obp-smells.md`

### 2a. In "## Naming — the name is the contract", a new first bullet

```markdown
- **A name is a path, never glued words.** A compound becomes a dot path, one word per segment (`setting.build.execution.path`, not `buildExecutionPath`), and the path must navigate: each segment an owner whose member is the next. See *glued name*.
```

### 2b. In "### Shape — objects & collections", a new first entry

```markdown
**glued name** — a compound camelCase name: `ListName`, `buildExecutionPath`, `MaxRedirects`, `TimeoutInSec`.

```csharp
public partial data.@this<app.type.item.variable.@this> ListName { get; init; }   // list.name: a list has no name
```

Fix: write it as a dot path and check it navigates. `list.name` doesn't (the name is the holding variable's, `list.variable.name`), so the parameter is the list itself: `data.@this<list> List`, its name reached through it. A path that navigates is the right place; one that doesn't points to the real owner.

Tell: a capital letter in the middle of a plang-visible name. The one sanctioned compound stays `IsX`/`HasX`.
```

## 3. Held for Ingi before it goes in: the template from the settings survey

The coder's survey of 20 setting files and 82 action options found six patterns. They go into 2b as "the template" once Ingi rules on 2, 4 and 6:
1. `Max`/`Min` + noun → `noun.max` (`MaxRedirects` → `redirect.max`).
2. A unit in a name → a typed value, and the unit leaves (`TimeoutInSec` → `timeout`, a `duration`). Changes a type.
3. Verb + noun → `noun.verb` (`IgnoreIfNotFound` → `missing.ignore`).
4. A negated verb → the positive bool with its default flipped (`SkipFreshnessCheck` → `freshness.check`, true by default).
5. A name that repeats its owner drops that part (`on.error`'s `IgnoreError` → `ignore`).
6. A shared prefix across siblings → one sub-record (`FollowRedirects`/`MaxRedirects` → `redirect.{follow, max}`). Changes a shape.
