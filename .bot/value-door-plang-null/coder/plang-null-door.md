# The value door should return a plang type, never CLR null

**Author:** coder
**Branch:** `value-door-plang-null` (cut from `settings-config-unification`)
**Status:** design writeup for architect review — no implementation yet
**Origin:** surfaced while converting the math module on `settings-config-unification`. Ingi
signed off on the principle; this captures the shape before we touch the whole codebase.

---

## The problem

The typed value door leaks CLR null into plang-type land:

```csharp
public ValueTask<T?> Value<T>() where T : item.@this, ICreate<T>   // returns T? — CLR nullable
```

`T.Create(...)` returns **CLR null** when the value can't become a `T` (e.g. `number.Create` on
`@null`/text → `this.cs:93` `case null: return null`). So every handler reads a value like this:

```csharp
var a = await action.A.Value<number>();
if (a == null) return Invalid(...);                 // CLR-null check
var overflow = (await action.Overflow.Value())!;    // (await …)!  — null-forgive a guaranteed value
```

Two smells, both pervasive (`list/remove`, `list/set`, `condition`, `event/on`, `crypto`, math, …):

1. **`== null`** — a CLR-null test sitting in code that is otherwise all plang types. `number` is a
   plang type; asking it and getting CLR `null` back drops out of the domain.
2. **`(await …Value())!`** — the null-forgiving `!` (plus mandatory parens for `await` precedence)
   appears on *every* guaranteed-present read. It's noise, and "everywhere" means it signals nothing.

## The insight (why this doesn't cost us the C# `?` benefit)

The instinct against changing it is "we'd lose the C# compiler's `?`/`!` null-safety." But:

> **`?`/`!` exist for exactly one job: stop a CLR-null NRE.**

In a plang-types-everywhere model the door never hands back CLR null — it hands back a real plang
instance, and **`@null` is a valid, typed instance** (the null model already has "one null citizen,
optionally typed" — a *null-number*, a *null-text*). So there is **no NRE to guard against**, which
means there is **nothing for `?`/`!` to protect**. You don't lose the benefit — you make it
*unnecessary*. `IsNull` then answers a **domain** question ("is this a real number?"), not a
**safety** one.

## The change

```csharp
public ValueTask<T> Value<T>() where T : item.@this, ICreate<T>    // returns T — never CLR null
```

- `T.Create(...)` returns a **typed `@null`** (a `T` with `IsNull == true`) on decline/absence,
  never CLR null. Applies to every type's `Create` (number, text, path, choice, @bool, …).
- Handlers read plang types end to end:

```csharp
var a = await action.A.Value<number>();
var b = await action.B.Value<number>();
if (a.IsNull || b.IsNull) return Invalid(action.Context, "add", "two numbers");   // domain check
var overflow  = await action.Overflow.Value();    // choice<Overflow>, present — no !
var precision = await action.Precision.Value();
return action.Context.Data(() => a.Add(b, overflow, precision));
```

No `== null`, no `(await …)!`, every value a plang type.

## Why this is a first-class, architect-reviewed change (not a plunge)

- **Big-bang, not incremental.** The moment `Value<T>()` stops returning null, every `== null`
  check in every handler silently becomes always-false — logic breaks. So the door **and all
  callers flip together**. There is no gradual path.
- **Whole-codebase scope.** ~100+ `Value<T>()`/`Value()` call sites + every type's `Create` +
  the `@null`-typed semantics + `IsNull` discipline. Far larger than one module.
- **Core value-model surface.** Touches the same door the whole runtime reads values through.

So it deserves the same rigor the settings unification got: this writeup → architect design →
staged build spec → coder.

## Open questions for the architect

1. **Typed `@null`.** Does `number.Create(@null-input)` return a `number` with `IsNull`, or the
   shared `@null.Instance`? (The handler needs `a.IsNull` to work *and* `a.Add(...)` to be safe on
   a null-number — so a **typed** null-number seems required. Confirm the null model supports
   `@null` typed as each `T`.)
2. **`type-mismatch` vs `absent` vs `@null`.** Today CLR null conflates three cases (A is `@null`,
   A is text "abc" that can't convert, A is absent). Under typed-`@null`, do they collapse to one
   `IsNull`, or does a genuine type-mismatch (text→number) need a distinct signal (an error Data,
   not a null-number)? This changes handler validation.
3. **`Value()` (untyped) + `Peek()`** — do they change too, or only `Value<T>()`? (`Peek()` already
   returns `@null.Instance` on absence — `this.cs:313` — so it's arguably already plang-null.)
4. **Sequencing vs settings.** `settings-config-unification` is mid-flight; its http/llm/signing
   conversions will *write* the `(await …)!`/`==null` pattern (matching the current norm). Do we (a)
   finish settings, then this sweep fixes everything uniformly (coder's lean — the debt is bounded
   and pre-existing), or (b) land this first so those conversions are clean first-time? Coder flags
   each such read on settings with a `// T? convention — plang-null pass converts this` note so it's
   known-debt, not endorsed style.
5. **Migration mechanics.** `== null` → `.IsNull` is not a blind sed (some `== null` are genuinely
   CLR-nullable refs, not value-door results). Needs a targeted pass keyed on `Value<...>()`/`.Value()`
   results. Worth an analyzer/roslyn-fix?

## Relation to `settings-config-unification`

This is the concrete shape of the "plang-types-everywhere" vision. The math module on that branch
is correct and idiomatic *for today's `T?` door*; when this lands, its `(await …)!` and `== null`
collapse to `await …` and `.IsNull`. Same for every http/llm/signing handler settings touches.
