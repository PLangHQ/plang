# For architect — Ingi's call: number arithmetic interfaces take `item`/`number`, not raw CLR (supersedes "arithmetic untouched")

**From:** coder. **2026-07-10.** Ingi reviewed the number re-key and flagged that the arithmetic
helpers take CLR primitives. **This SUPERSEDES the "Arithmetic/Equality/Operators/Axes keep their
logic UNTOUCHED" line** in `stage2-number-final-shape.md` — Ingi chose to re-type them now (option B),
not defer to the plang-types-everywhere branch.

## What changes

The low-level arithmetic helpers currently take raw CLR and lower at the CALL site:

```csharp
// before — CLR at the interface:
private static @this DoubleOp(double a, double b, ArithOp op) => op switch { Add => (@this)(a + b), … };
// caller:  DoubleOp(a.AsDouble(), b.AsDouble(), op)
```

They become **item-typed at the interface**, lowering only at the actual `+`/`*` .NET boundary:

```csharp
// after — plang types at the interface, CLR only at the op:
private static @this DoubleOp(@this a, @this b, ArithOp op) => op switch { Add => (@this)(a.AsDouble() + b.AsDouble()), … };
// caller:  DoubleOp(a, b, op)
```

The lowering (`AsDouble`/`AsDecimal`/`AsBigInteger`) doesn't disappear — a `+` IS a .NET op — it just
moves INSIDE the helper (the one real boundary), so no interface passes a bare `double`/`decimal`/
`BigInteger`. Scope: `BigOp`, `DoubleOp`, `DecimalOp`, `DivDouble`, `DivDecimal` in `this.Arithmetic.cs`
(the top-level `DoOp`/`DoAbs` already take `@this`); the unary/equality helpers were already re-shaped in
the re-key.

## Why B (Ingi)

Plang types at every interface is the rule; CLR appears only at the genuine .NET perimeter (the
arithmetic operator, `IWriter`, `Clr<T>()`). The perf gate that deferred plang-types-everywhere doesn't
bite here — these helpers already lower to CLR immediately, so the item-typed interface adds one wrapper
per op at most, and the hot path (the `+` itself) is unchanged.

## Acceptance (unchanged intent)

Arithmetic suite stays green — this is an interface re-type, not a logic change: the same
`AsX()` lowering, the same operator, byte-for-byte results.
