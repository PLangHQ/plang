# Values are born through the context

## The lie this removes

`Data.Context` (`data/this.cs:114-123`) is declared non-null but backed nullable:

```
public actor.context.@this Context
{
    get => _context ?? (_type as module.IContext)?.Context!;   // the trailing ! returns null at runtime
    set { _context = value; if (value != null && _type is IContext c) c.Context = value; }
}
```

The getter falls back to the value's own context, and if neither the wrapper nor the value carries one it returns null despite the non-null declaration. The setter "propagates only a non-null context downward." Both halves exist because today a value is constructed first and stamped with context later — the gap between the two is the nullable window. Close the gap and the `!` and the null-guarded setter both go.

## The seven value types

`type`, `dict`, `list`, `path`, `clr`, `computed`, `source` each carry a `Context` field/property. `path` is the load-bearing case — it needs context to call `AuthGate` on a verb, so a raw `path` outside any Data wrapper still must know its context. So the value genuinely carries context; the question is only whether it can ever be null. It cannot, if every birth supplies one.

## Born through the context (the factory model)

A value is born from the context that is already in hand, not constructed-then-stamped:

- `context.Null()` replaces `Data.Null()` / `@null.@this.Instance` as a sentinel source.
- `context.Error(...)` replaces context-less error construction.
- `context.Ok(value)` / equivalents replace `Data.Ok(value)` where a context is in scope.

No System-context floor. Ingi was explicit: if a birth needs context, the birth knows its context — we do not paper over a missing one with a shared fallback. Every real birth already has a context:

- handler result — the handler holds its action context.
- `%var%` resolve — the variable holds its context.
- deserialize — the serializer holds its context (mechanism C).
- LLM parse — the build/run pipeline holds its context.

## Sentinels

`Data.Null()` (`data/this.cs:534`) builds `new(name, @null.@this.Instance)` — a static singleton, context-free, born before any App exists. Under the factory model this becomes `context.Null()`. Any other `static readonly` value sentinel moves the same way: it cannot be a static field born ahead of the App; it is minted from the context at the call site. If a genuinely shared, immutable sentinel must outlive contexts, that is the one place to raise before assuming a factory fits — but `Null` does not need to.

## The two reflection births

Only two `Activator.CreateInstance` sites build a plang value, and both already hold a context in scope. They stamp `.Context` immediately after construction — Ingi confirmed stamping is enough; the point is the field type is `Context`, not `Context?`.

**Site 1 — `type.cs:391`, `choice<TEnum>` from a CLR enum.**

```
type.@this.OfRaw(raw, kind, context)          // context is a parameter
  └─ raw is System.Enum
       └─ Activator.CreateInstance(choiceType, raw)   // :391 — stamp .Context = context after
```

The method already uses `context` on the surrounding lines (`:383` OfStatic, `:395` `new Clr(raw) { Context = context }`). The enum branch is the lone sibling that forgot to stamp.

**Site 2 — `Wire.cs:265`, `Data<T>` rebuild (`WrapAsTyped`).**

```
Wire (instance, holds _context)
  └─ ReadTyped at :193
       └─ WrapAsTyped(bodyData, typeToConvert)   // static helper
            └─ Activator.CreateInstance(targetType, Type.Missing…)   // :265 — stamp from _context
```

Called exactly once, from `:193`, inside the serializer Read where `_context` lives (non-null after mechanism C). Pass `_context` to the helper (or make it an instance method) and stamp.

## Not values — leave them alone

The other `Activator.CreateInstance` sites build CLR intermediates or converters, never plang values, and carry no context:

- `Conversion.cs:117,150,374,381` — CLR `List<T>` and `default(valuetype)` intermediates, wrapped into context-bearing plang values afterward.
- `JsonString.cs:257`, `choice/Json.cs:19` — `JsonConverter` instances.

## You own the final shape

`context.Null/Error/Ok` are the intended factory surface; the exact method set and names are the coder's call. The contract is: a value is never observable without a context, and the `?` is gone from the seven value types and `Error`.
