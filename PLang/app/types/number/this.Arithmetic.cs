namespace app.types.number;

/// <summary>
/// Policy-aware arithmetic surface — what the <c>math.*</c> handlers call.
/// Each method returns <see cref="global::app.data.@this{T}"/> wrapping a
/// <see cref="@this"/>; <see cref="System.OverflowException"/> and
/// <see cref="System.DivideByZeroException"/> are caught internally and
/// surface as <c>Data.Fail("MathOverflow")</c> / <c>"DivideByZero"</c>.
///
/// <para>Promotion table for + - * %:
///   <c>Int × Int → Int</c>; either Long → Long; either Decimal → Decimal;
///   either Double → Double. Decimal × Double promotes by
///   <see cref="PrecisionMode"/>.</para>
///
/// <para>Divide leaves the integer track — <c>7 / 2 → 3.5</c> as Decimal
/// (lenient) or Double (when precision is Double-leaning). Truncating
/// division is the explicit <c>math.intdiv</c> action.</para>
///
/// <para>Power promotes by exponent shape: integer base + non-negative
/// integer exponent stays integer (overflow per policy); negative exponent
/// leaves the integer track; fractional exponent is always Double.</para>
/// </summary>
public sealed partial class @this
{
    public static global::app.data.@this<@this> Add(@this a, @this b, NumberPolicy policy)
        => Wrap(() => DoOp(a, b, ArithOp.Add, policy));

    public static global::app.data.@this<@this> Subtract(@this a, @this b, NumberPolicy policy)
        => Wrap(() => DoOp(a, b, ArithOp.Sub, policy));

    public static global::app.data.@this<@this> Multiply(@this a, @this b, NumberPolicy policy)
        => Wrap(() => DoOp(a, b, ArithOp.Mul, policy));

    public static global::app.data.@this<@this> Modulo(@this a, @this b, NumberPolicy policy)
        => Wrap(() => DoOp(a, b, ArithOp.Mod, policy));

    public static global::app.data.@this<@this> Divide(@this a, @this b, NumberPolicy policy)
        => Wrap(() => DoDivide(a, b, policy));

    public static global::app.data.@this<@this> IntDivide(@this a, @this b, NumberPolicy policy)
        => Wrap(() => DoIntDivide(a, b));

    public static global::app.data.@this<@this> Power(@this a, @this b, NumberPolicy policy)
        => Wrap(() => DoPower(a, b, policy));

    private static global::app.data.@this<@this> Wrap(System.Func<@this> compute)
    {
        try { return global::app.data.@this<@this>.Ok(compute()); }
        catch (System.DivideByZeroException ex)
        {
            return global::app.data.@this<@this>.FromError(
                new global::app.errors.ServiceError(ex.Message, "DivideByZero", 400) { Exception = ex });
        }
        catch (System.OverflowException ex)
        {
            return global::app.data.@this<@this>.FromError(
                new global::app.errors.ServiceError(ex.Message, "MathOverflow", 400) { Exception = ex });
        }
        catch (System.ArithmeticException ex)
        {
            return global::app.data.@this<@this>.FromError(
                new global::app.errors.ServiceError(ex.Message, "ArithmeticError", 400) { Exception = ex });
        }
    }

    private enum ArithOp { Add, Sub, Mul, Mod }

    private static @this DoOp(@this a, @this b, ArithOp op, NumberPolicy policy)
    {
        var kind = PromoteKind(a.Kind, b.Kind, policy);
        try
        {
            return kind switch
            {
                NumberKind.Int => DoIntKind(a.AsInt64(), b.AsInt64(), op, asInt: true, policy),
                NumberKind.Long => DoIntKind(a.AsInt64(), b.AsInt64(), op, asInt: false, policy),
                NumberKind.Decimal => ArithDecimal(a.AsDecimal(), b.AsDecimal(), op),
                NumberKind.Double or NumberKind.Float => ArithDouble(a.AsDouble(), b.AsDouble(), op),
                _ => throw new System.InvalidOperationException(),
            };
        }
        catch (System.OverflowException) when (policy.Overflow == OverflowMode.Promote && kind == NumberKind.Int)
        {
            // Int overflow widens to Long.
            return DoOp(@this.From(a.AsInt64()), @this.From(b.AsInt64()), op,
                policy with { /* same policy */ });
        }
        catch (System.OverflowException) when (policy.Overflow == OverflowMode.Promote && kind == NumberKind.Long)
        {
            // Long overflow widens to Decimal.
            return ArithDecimal(a.AsDecimal(), b.AsDecimal(), op);
        }
    }

    private static @this DoIntKind(long a, long b, ArithOp op, bool asInt, NumberPolicy policy)
    {
        if (asInt)
        {
            // Promoted kind is Int — keep the checked op at int width so int
            // overflow surfaces. The DoOp catch widens to Long under Promote;
            // under Throw the exception propagates to the Data.Fail wrapper.
            int ia = checked((int)a), ib = checked((int)b);
            int ri = checked(op switch
            {
                ArithOp.Add => ia + ib,
                ArithOp.Sub => ia - ib,
                ArithOp.Mul => ia * ib,
                ArithOp.Mod => ia == int.MinValue && ib == -1 ? throw new System.OverflowException()
                           : (ib == 0 ? throw new System.DivideByZeroException() : ia % ib),
                _ => throw new System.InvalidOperationException()
            });
            return From(ri);
        }

        long r = checked(op switch
        {
            ArithOp.Add => a + b,
            ArithOp.Sub => a - b,
            ArithOp.Mul => a * b,
            ArithOp.Mod => a == long.MinValue && b == -1 ? throw new System.OverflowException()
                       : (b == 0 ? throw new System.DivideByZeroException() : a % b),
            _ => throw new System.InvalidOperationException()
        });
        return From(r);
    }

    private static @this ArithDecimal(decimal a, decimal b, ArithOp op) => op switch
    {
        ArithOp.Add => From(a + b),
        ArithOp.Sub => From(a - b),
        ArithOp.Mul => From(a * b),
        ArithOp.Mod => b == 0m ? throw new System.DivideByZeroException() : From(a % b),
        _ => throw new System.InvalidOperationException(),
    };

    private static @this ArithDouble(double a, double b, ArithOp op) => op switch
    {
        ArithOp.Add => From(a + b),
        ArithOp.Sub => From(a - b),
        ArithOp.Mul => From(a * b),
        ArithOp.Mod => From(a % b),
        _ => throw new System.InvalidOperationException(),
    };

    private static @this DoDivide(@this a, @this b, NumberPolicy policy)
    {
        var kind = PromoteKind(a.Kind, b.Kind, policy);
        // Divide leaves the integer track — promote Int/Long.
        if (kind == NumberKind.Int || kind == NumberKind.Long)
            kind = policy.Precision == PrecisionMode.Decimal
                ? NumberKind.Decimal
                : NumberKind.Decimal; // lenient: prefer Decimal for 7/2 → 3.5

        return kind switch
        {
            NumberKind.Decimal => b.AsDecimal() == 0m
                ? throw new System.DivideByZeroException()
                : From(a.AsDecimal() / b.AsDecimal()),
            NumberKind.Double or NumberKind.Float =>
                From(a.AsDouble() / b.AsDouble()),
            _ => throw new System.InvalidOperationException(),
        };
    }

    private static @this DoIntDivide(@this a, @this b)
    {
        // Truncating division — the explicit opt-in for the C# semantics.
        long bi = b.AsInt64();
        if (bi == 0) throw new System.DivideByZeroException();
        long r = a.AsInt64() / bi;
        return (a.Kind == NumberKind.Int && b.Kind == NumberKind.Int
                && r >= int.MinValue && r <= int.MaxValue)
            ? From((int)r) : From(r);
    }

    private static @this DoPower(@this a, @this baseExp, NumberPolicy policy)
    {
        // Fractional exponent ⇒ Double IEEE math.
        bool exponentIsFractional =
            (baseExp.Kind == NumberKind.Decimal && baseExp.AsDecimal() != System.Math.Truncate(baseExp.AsDecimal()))
            || ((baseExp.Kind == NumberKind.Double || baseExp.Kind == NumberKind.Float)
                && baseExp.AsDouble() != System.Math.Truncate(baseExp.AsDouble()));

        if (exponentIsFractional)
            return From(System.Math.Pow(a.AsDouble(), baseExp.AsDouble()));

        // Negative integer exponent leaves integer track.
        long expL = baseExp.AsInt64();
        if (expL < 0)
        {
            // Promote per precision policy.
            if (policy.Precision == PrecisionMode.Decimal
                && a.Kind != NumberKind.Double && a.Kind != NumberKind.Float)
            {
                decimal acc = 1m, baseD = a.AsDecimal();
                for (long i = 0; i < -expL; i++) acc /= baseD;
                return From(acc);
            }
            return From(System.Math.Pow(a.AsDouble(), expL));
        }

        // Non-negative integer exponent on integer base — stay integer, overflow per policy.
        if ((a.Kind == NumberKind.Int || a.Kind == NumberKind.Long)
            && (baseExp.Kind == NumberKind.Int || baseExp.Kind == NumberKind.Long))
        {
            try
            {
                long b = a.AsInt64();
                long r = 1;
                for (long i = 0; i < expL; i++) r = checked(r * b);
                return (a.Kind == NumberKind.Int && r >= int.MinValue && r <= int.MaxValue)
                    ? From((int)r) : From(r);
            }
            catch (System.OverflowException) when (policy.Overflow == OverflowMode.Promote)
            {
                // Widen to decimal.
                decimal acc = 1m, baseD = a.AsDecimal();
                for (long i = 0; i < expL; i++) acc *= baseD;
                return From(acc);
            }
        }

        // Decimal or Double base — route through the matching path.
        if (a.Kind == NumberKind.Decimal && expL >= 0)
        {
            decimal acc = 1m, baseD = a.AsDecimal();
            for (long i = 0; i < expL; i++) acc *= baseD;
            return From(acc);
        }
        return From(System.Math.Pow(a.AsDouble(), expL));
    }

    /// <summary>
    /// Promotion under policy. Decimal × Double swings on
    /// <see cref="PrecisionMode"/>.
    /// </summary>
    private static NumberKind PromoteKind(NumberKind a, NumberKind b, NumberPolicy policy)
    {
        bool aDec = a == NumberKind.Decimal, bDec = b == NumberKind.Decimal;
        bool aDbl = a == NumberKind.Double || a == NumberKind.Float;
        bool bDbl = b == NumberKind.Double || b == NumberKind.Float;

        if (aDec && bDbl) return policy.Precision == PrecisionMode.Decimal ? NumberKind.Decimal : NumberKind.Double;
        if (bDec && aDbl) return policy.Precision == PrecisionMode.Decimal ? NumberKind.Decimal : NumberKind.Double;
        if (aDbl || bDbl) return NumberKind.Double;
        if (aDec || bDec) return NumberKind.Decimal;
        if (a == NumberKind.Long || b == NumberKind.Long) return NumberKind.Long;
        return NumberKind.Int;
    }
}
