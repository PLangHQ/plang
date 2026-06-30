# Crash #3 — two distinct problems (correcting an over-collapse)

**Branch:** `fix-plang-build`. Supersedes the earlier `crash3-is-crash2-provenance.md`, which over-claimed "#3 is #2". Responds to `coder/plang-build-findings.md` crash #3.
**Why:** the coder listed #2 and #3 as **two** problems. I folded #3 into #2 on the strength of one build run that hit the StackOverflow. Re-dug by running both in isolation — there are genuinely two, with two different fixes, neither subsuming the other.

## Why one spot showed two faces

`path` on the build path failed as an ArgumentException for the coder and as a StackOverflow for me. The difference is the `.pr` type-name hand-edit (`3b52d6797`) that landed between the runs: it un-broke step 0 (`set default %path% = "/"`), so the build now gets *past* it to the seam clobber (problem A) instead of dying earlier on an absent `path` (problem B). Same location, two mechanisms.

## Problem A — self-ref clobber → StackOverflow (the #2 family; also the build's current proximate cause)

Confirmed by running the real builder (core-dump, `SIGABRT`) and in isolation. The chain, each link run:

1. **Step 0** `set default %path% = "/"` stores `path = "/"` (the spurious `Type=string` in the `.pr` is harmless — `string` resolves to text).
2. **Step 4** `call EmitBuildEvent ... path=%path%` **clobbers `path` with the bare self-ref**. Driven through the real `goal.call` (`Call` → `RunGoalAsync`), observed via `Peek()`: `before=/ → after.isSelfRef=True after.raw=%path%`.
3. EmitBuildEvent renders `%path%` → `Get(path)` → self-alias → `text.Value ⇄ data.Value` SO.

The arming write is `app/this.cs:585`, the call-by-value param seam:

```csharp
param.Context = context;
if (param.Peek() is global::app.variable.@this)          // ← guard fires on a name-slot
    await context.Variable.Set(param.Name,
        new global::app.data.@this(param.Name, await param.Value(), context: context));
else
    await context.Variable.Set(param.Name, param);        // ← stores the raw self-ref
```

The author wrote the `if` to resolve a ref before storing (the comment names the `x=%x%` loop), but the predicate asks "is this a `variable` name-slot?" when it means "is this a live full-match reference?". `build.pr` types `path=%path%` as `text`, so the live ref skips the `if` and the `else` stores the bare `%path%`.

**Fix:** the write-door collapse from `crash2-stackoverflow.md` — canonicalize a full-match `%ref%` to its current instance inside `Variable.Set`. The seam's `else` then stores `path`'s current `"/"`, no self-ref, no SO. Patching only the seam predicate (`if (param.IsVariable)`) fixes this path but leaves the read door one forgotten writer away from the same SO; the door owns the invariant.

## Problem B — unset path ref → hard ArgumentException (the genuinely separate #3)

`path.Value` (`path/this.cs:110`):

```csharp
var rendered = (await _location.Value(data)).Clr<string>() ?? "";   // absent resolution → ""
return Resolve(rendered, data.Context ?? Context!);                  // Resolve("")
```

When the location is a live `%ref%` that resolves to **absent**, text answers `Absent`, `.Clr<string>()` is null, `?? ""` makes it empty, and `Resolve("")` → `ValidatePath("")` hard-throws (`file/this.Validate.cs:32`):

```csharp
if (string.IsNullOrWhiteSpace(path))
    throw new ArgumentException("path cannot be empty", nameof(path));
```

This is **not** the self-ref SO and **not** build-specific — any unset path variable in any goal hits it. Reproduced in isolation (no `goal.call`, no self-ref): a path whose location is a stamped `%missing%` (built via the `Raw` init, since runtime `new Data(.., "%missing%", path)` literalizes the location instead of stamping it), then `await data.Value()`:

```csharp
var fp = new global::app.type.path.file.@this("/seed", ctx) { Raw = "%missing%" };
var pathData = new global::app.data.@this("p", fp, context: ctx);
await pathData.Value();
// → ArgumentException: "path cannot be empty (Parameter 'path')"
```

**Fix:** `path.Value` must surface a failed `_location` resolution as a typed `Data` error (the `VariableNotFound` text already raised on the binding), not coerce it to `""` and let `ValidatePath` throw. A raw `ArgumentException` for an unresolved path is the wrong shape — an unset path ref is a developer error to report, not a courier throw.

## The two are independent

A's fix stops the self-ref clobber, so the build resolves `path` to `"/"`. But a developer who genuinely leaves a path variable unset still hits B. B's fix gives a clean typed error but does nothing about the self-ref loop. Both are needed.
