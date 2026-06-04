using System.Numerics;

namespace app.type.number;

/// <summary>
/// Policy-aware arithmetic — what the <c>math.*</c> handlers call. Way 3:
/// integers compute on a <c>BigInteger</c> carrier and narrow to the smallest
/// kind that fits (Promote) or stay strict-width (Throw); binary floats compute
/// in <c>double</c>; decimals in <c>decimal</c>; the <c>double ⊕ decimal</c> mix
/// is resolved by <see cref="PrecisionMode"/> (Error by default). Each method
/// returns <see cref="global::app.data.@this{T}"/>; exceptions surface as typed
/// <c>Data.Fail</c>.
/// </summary>
public sealed partial class @this
{
    /// <summary>Loop-protective cap on integer-track power exponents (CPU-DoS guard).</summary>
    public const long MaxPowerExponent = 64;

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
        => Wrap(() => DoIntDivide(a, b, policy));
    public static global::app.data.@this<@this> Power(@this a, @this b, NumberPolicy policy)
        => Wrap(() => DoPower(a, b, policy));

    private static global::app.data.@this<@this> Wrap(System.Func<@this> compute)
    {
        try { return global::app.data.@this<@this>.Ok(compute()); }
        catch (System.DivideByZeroException ex)
        {
            return global::app.data.@this<@this>.FromError(
                new global::app.error.ServiceError(ex.Message, "DivideByZero", 400) { Exception = ex });
        }
        catch (PrecisionMixException ex)
        {
            return global::app.data.@this<@this>.FromError(
                new global::app.error.ServiceError(ex.Message, "PrecisionMixRequiresChoice", 400) { Exception = ex });
        }
        catch (System.OverflowException ex)
        {
            return global::app.data.@this<@this>.FromError(
                new global::app.error.ServiceError(ex.Message, "MathOverflow", 400) { Exception = ex });
        }
        catch (PowerExponentTooLargeException ex)
        {
            return global::app.data.@this<@this>.FromError(
                new global::app.error.ServiceError(ex.Message, "PowerExponentTooLarge", 400) { Exception = ex });
        }
        catch (System.ArithmeticException ex)
        {
            return global::app.data.@this<@this>.FromError(
                new global::app.error.ServiceError(ex.Message, "ArithmeticError", 400) { Exception = ex });
        }
    }

    private sealed class PowerExponentTooLargeException : System.Exception
    {
        public PowerExponentTooLargeException(string message) : base(message) { }
    }

    /// <summary>The double⊕decimal mix with no precision choice — Way 3's "correct, not easy" edge.</summary>
    private sealed class PrecisionMixException : System.ArithmeticException
    {
        public PrecisionMixException()
            : base("Mixing a double and a decimal needs an explicit precision choice — "
                   + "neither represents the other exactly. Set math.number.precision = double | decimal "
                   + "(or pass precision on the step).") { }
    }

    internal enum ArithOp { Add, Sub, Mul, Mod }

    private static @this DoOp(@this a, @this b, ArithOp op, NumberPolicy policy)
    {
        Category ca = a.Cat, cb = b.Cat;

        // integer ⊕ integer — BigInteger carrier, narrow per policy.
        if (ca == Category.Integer && cb == Category.Integer)
        {
            BigInteger r = BigOp(a.AsBigInteger(), b.AsBigInteger(), op);
            NumberKind floor = WiderInteger(a.Kind, b.Kind);
            return policy.Overflow == OverflowMode.Throw ? NarrowStrict(r, floor) : NarrowInteger(r, floor);
        }

        bool aBF = ca == Category.BinaryFloat, bBF = cb == Category.BinaryFloat;
        bool aDec = ca == Category.Decimal, bDec = cb == Category.Decimal;

        // double ⊕ decimal — the precision-policy fork.
        if ((aBF && bDec) || (aDec && bBF))
            return policy.Precision switch
            {
                PrecisionMode.Double => DoubleOp(a.AsDouble(), b.AsDouble(), op),
                PrecisionMode.Decimal => DecimalOp(a.AsDecimal(), b.AsDecimal(), op),
                _ => throw new PrecisionMixException(),
            };

        // any binary float (with integer or another binary float) → double.
        if (aBF || bBF) return DoubleOp(a.AsDouble(), b.AsDouble(), op);

        // remaining: decimal with integer/decimal → decimal.
        return DecimalOp(a.AsDecimal(), b.AsDecimal(), op);
    }

    private static BigInteger BigOp(BigInteger a, BigInteger b, ArithOp op) => op switch
    {
        ArithOp.Add => a + b,
        ArithOp.Sub => a - b,
        ArithOp.Mul => a * b,
        ArithOp.Mod => b == BigInteger.Zero ? throw new System.DivideByZeroException() : a % b,
        _ => throw new System.InvalidOperationException(),
    };

    private static @this DecimalOp(decimal a, decimal b, ArithOp op) => op switch
    {
        ArithOp.Add => From(a + b),
        ArithOp.Sub => From(a - b),
        ArithOp.Mul => From(a * b),
        ArithOp.Mod => b == 0m ? throw new System.DivideByZeroException() : From(a % b),
        _ => throw new System.InvalidOperationException(),
    };

    private static @this DoubleOp(double a, double b, ArithOp op) => op switch
    {
        ArithOp.Add => From(a + b),
        ArithOp.Sub => From(a - b),
        ArithOp.Mul => From(a * b),
        ArithOp.Mod => From(a % b),
        _ => throw new System.InvalidOperationException(),
    };

    private static @this DoDivide(@this a, @this b, NumberPolicy policy)
    {
        Category ca = a.Cat, cb = b.Cat;
        bool aBF = ca == Category.BinaryFloat, bBF = cb == Category.BinaryFloat;
        bool aDec = ca == Category.Decimal, bDec = cb == Category.Decimal;

        if ((aBF && bDec) || (aDec && bBF))
            return policy.Precision switch
            {
                PrecisionMode.Double => DivDouble(a, b),
                PrecisionMode.Decimal => DivDecimal(a, b),
                _ => throw new PrecisionMixException(),
            };

        if (aBF || bBF) return DivDouble(a, b);

        // integer/integer, integer/decimal, decimal/decimal — division leaves
        // the integer track to decimal (7/2 → 3.5).
        return DivDecimal(a, b);
    }

    private static @this DivDouble(@this a, @this b) => From(a.AsDouble() / b.AsDouble());

    private static @this DivDecimal(@this a, @this b)
    {
        decimal bd = b.AsDecimal();
        if (bd == 0m) throw new System.DivideByZeroException();
        return From(a.AsDecimal() / bd);
    }

    private static @this DoIntDivide(@this a, @this b, NumberPolicy policy)
    {
        // Truncating division — explicit C# semantics. Integer operands only;
        // result stays integer (narrowed per policy from the wider operand kind).
        if (a.Cat != Category.Integer || b.Cat != Category.Integer)
            throw new System.ArithmeticException("intdiv requires integer operands.");
        BigInteger bb = b.AsBigInteger();
        if (bb == BigInteger.Zero) throw new System.DivideByZeroException();
        BigInteger r = a.AsBigInteger() / bb;
        NumberKind floor = WiderInteger(a.Kind, b.Kind);
        return policy.Overflow == OverflowMode.Throw ? NarrowStrict(r, floor) : NarrowInteger(r, floor);
    }

    private static @this DoPower(@this a, @this exp, NumberPolicy policy)
    {
        // Fractional exponent ⇒ double IEEE math.
        bool exponentIsFractional =
            (exp.Cat == Category.Decimal && exp.AsDecimal() != System.Math.Truncate(exp.AsDecimal()))
            || (exp.Cat == Category.BinaryFloat && exp.AsDouble() != System.Math.Truncate(exp.AsDouble()));
        if (exponentIsFractional)
            return From(System.Math.Pow(a.AsDouble(), exp.AsDouble()));

        long expL = exp.ToInt64();

        if (expL < 0)
        {
            if (policy.Precision == PrecisionMode.Decimal && a.Cat != Category.BinaryFloat)
            {
                EnsureExponentInRange(expL);
                decimal acc = 1m, baseD = a.AsDecimal();
                for (long i = 0; i < -expL; i++) acc /= baseD;
                return From(acc);
            }
            return From(System.Math.Pow(a.AsDouble(), expL));
        }

        // Non-negative integer exponent on an integer base — BigInteger carrier, narrow per policy.
        if (a.Cat == Category.Integer)
        {
            EnsureExponentInRange(expL);
            BigInteger r = BigInteger.Pow(a.AsBigInteger(), (int)expL);
            NumberKind floor = a.Kind;
            return policy.Overflow == OverflowMode.Throw ? NarrowStrict(r, floor) : NarrowInteger(r, floor);
        }

        // Decimal base, non-negative integer exponent — repeated decimal multiply.
        if (a.Cat == Category.Decimal)
        {
            EnsureExponentInRange(expL);
            decimal acc = 1m, baseD = a.AsDecimal();
            for (long i = 0; i < expL; i++) acc *= baseD;
            return From(acc);
        }

        // Binary-float base — Math.Pow is constant-time.
        return From(System.Math.Pow(a.AsDouble(), expL));
    }

    private static void EnsureExponentInRange(long expL)
    {
        if (expL > MaxPowerExponent || expL < -MaxPowerExponent)
            throw new PowerExponentTooLargeException(
                $"Integer-exponent magnitude {expL} exceeds limit {MaxPowerExponent}.");
    }
}
