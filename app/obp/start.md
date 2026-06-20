# OBP — Object-Based Pattern

OBP is one rule: **behavior lives on the owner**.

Everything else follows from that.

---

## the rules

### 1. behavior belongs to the owner

The object that owns the data owns all operations on that data.
If a method touches a `goal`, it belongs on `goal`. If it loops a `step.list`, it belongs on `step.list`.

```csharp
// wrong — caller iterates what step.list owns
foreach (var s in goal.steps)
    await s.start();

// right — step.list owns the loop
await goal.steps.start();
```

**Test:** whose data does this method touch? That's where it lives.

---

### 2. Data<T> in, data.@this out

Every method boundary uses PLang types. No raw C# types cross a signature.

```csharp
// wrong
void save(string name, int index) { }

// right
Task<data.@this> save(data.@this<text.@this> name, data.@this<number.@this> index)
```

PLang types:

| use | never |
|-----|-------|
| `data.@this<text.@this>` | `string` |
| `data.@this<path.@this>` | `string` (path) |
| `data.@this<number.@this>` | `int`, `long`, `double` |
| `data.@this<@bool.@this>` | `bool` |
| `data.@this<dict.@this>` | `Dictionary<,>` |
| `list<T>` | `IReadOnlyList<T>`, `IEnumerable<T>` |

Returns are `Task<data.@this>`. Never `void`, never raw C# types.

---

### 3. navigate, don't pass

Reach through the object graph. Never decompose an object into separate parameters.

```csharp
// wrong — caller decomposes file before passing
find(file.path, file.name)

// right — pass the object, let the receiver navigate
find(file)
```

And the receiver decides what it needs — not the caller.

---

### 4. names describe what the object IS

Properties are nouns. Methods are verbs. The name tells you what the thing is.

```
goal.steps        — "the steps"
step.actions      — "the actions"
file.path         — "the path"
```

Not:
```
goal.getSteps()   — "get" is redundant, steps IS the thing
manager.run()     — manager of what?
io.process()      — process what?
```

**Rule:** if you need `Get`/`Manager`/`Service`/`Helper` in the name, the shape is wrong.
The method is missing from the type that owns the data.

---

### 5. the list owns the loop

Every collection is its own type. It owns iteration, short-circuiting, and ordering.
The parent delegates — it never loops directly.

```csharp
// wrong — parent loops
foreach (var a in step.actions)
    await a.start();

// right — list owns the loop
await step.actions.start();
```

The list is named for what it contains:

```csharp
// wrong
list(list<goal> items)

// right
list(list<goal> goals)
```

---

### 6. the item knows itself

A list never reaches inside an item to match it.
The item answers questions about itself.

```csharp
// wrong — list decomposes item
files.first(f => f.path.Equals(target))

// right — item knows if it matches
files.first(f => f.is(target))
```

---

### 7. only leaves touch Data.Value

`Data` is a closed package: `{type, value, properties, signature}`.
Courier layers move it, inspect `.Success`, read `.Type` for routing.
They never open `.Value`.

Two things are leaves:
1. **Actions** that declare `Data<T>` — they named the type, they own the open.
2. **Serializers** — the type renders itself per format.

Everything between is courier. A courier that opens the package to branch on `.Value` is a leaf pretending to be a relay.

```csharp
// wrong — courier opens the package
if (data.Value is image img) { ... }

// right — declare the type at the leaf
public Task<data.@this> start(data.@this<image.@this> img) { ... }
```

---

### 8. relay data, don't repackage

`Data` flows. Intermediate layers pass it through unchanged.

```csharp
// wrong — loses type, properties, signature
return data.@this.Ok(result.Value);

// right
return result;
```

---

### 9. no redundant wrappers

If the callee needs what the caller already has, pass the object.
Don't construct a new type that copies the same fields into a different shape.

```csharp
// wrong
var ctx = new EventContext { GoalName = goal.name, Step = step };
await events.run(ctx);

// right
await events.run(goal, step);
```

---

## the smell list

Run this on any method or type. A "yes" means a type is missing — the fix is structural.

1. **Caller iterates a collection it doesn't own** → the loop belongs on the collection
2. **Caller decomposes an object before passing** → pass the object
3. **Raw C# type crosses a method boundary** → wrap in `Data<T>`
4. **`GetX`, `Manager`, `Service`, `Helper` in a name** → behavior on the wrong type
5. **Courier reads `.Value`** → declare the type at the leaf or forward as-is
6. **Same transform at 3+ call sites** (`f.path.TrimStart('/')` everywhere) → property on the owner
7. **Two fields that mirror a reference** (`goal.name` + `goal.goal.name`) → delete the flat copy
8. **`IReadOnlyList<T>` or `IEnumerable<T>` in a signature** → use `list<T>`
9. **Constructor does I/O** → defer to first access
10. **`lock(other.X)` from outside X** → the lock belongs on X

---

## module rules

Concept-specific OBP rules live here:

- [file](module/file/start.md)
- [goal](module/goal/start.md)
- [step](module/step/start.md)
- [action](module/action/start.md)
- [data](module/data/start.md)
