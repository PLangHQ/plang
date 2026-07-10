using System.Numerics;

namespace app.type.number;

/// <summary>
/// Policy-aware arithmetic — what the <c>math.*</c> handlers call. Way 3:
/// integers compute on a <c>BigInteger</c> carrier and narrow to the smallest
/// kind that fits (Promote) or stay strict-width (Throw); binary floats compute
/// in <c>double</c>; decimals in <c>decimal</c>; the <c>double ⊕ decimal</c> mix
/// is resolved by <see cref="Precision"/> (Error by default). Each method
/// returns the <see cref="@this"/> VALUE and THROWS a keyed AppException on error
/// (no context here to born a Data); the context-ful <c>math.*</c> handler turns the
/// throw into a native plang Data error.
/// </summary>
public sealed partial class @this
{
    /// <summary>Loop-protective cap on integer-track power exponents (CPU-DoS guard).</summary>
    public const long MaxPowerExponent = 64;

    // Instance ops — the op on the carrier (a.Add(b, …)); the other operand + the overflow/
    // precision settings ride as whole plang values (choice). The op unwraps them at the leaf
    // (the internal DoOp takes the enum; choice → enum is implicit) — no decompose at the call.
    public @this Add(@this b, global::app.type.item.choice.@this<Overflow> overflow, global::app.type.item.choice.@this<Precision> precision)
        => Wrap(() => DoOp(this, b, ArithOp.Add, overflow, precision));
    public @this Subtract(@this b, global::app.type.item.choice.@this<Overflow> overflow, global::app.type.item.choice.@this<Precision> precision)
        => Wrap(() => DoOp(this, b, ArithOp.Sub, overflow, precision));
    public @this Multiply(@this b, global::app.type.item.choice.@this<Overflow> overflow, global::app.type.item.choice.@this<Precision> precision)
        => Wrap(() => DoOp(this, b, ArithOp.Mul, overflow, precision));
    public @this Modulo(@this b, global::app.type.item.choice.@this<Overflow> overflow, global::app.type.item.choice.@this<Precision> precision)
        => Wrap(() => DoOp(this, b, ArithOp.Mod, overflow, precision));
    public @this Divide(@this b, global::app.type.item.choice.@this<Overflow> overflow, global::app.type.item.choice.@this<Precision> precision)
        => Wrap(() => DoDivide(this, b, precision));
    public @this IntDivide(@this b, global::app.type.item.choice.@this<Overflow> overflow, global::app.type.item.choice.@this<Precision> precision)
        => Wrap(() => DoIntDivide(this, b, overflow));
    public @this Power(@this exponent, global::app.type.item.choice.@this<Overflow> overflow, global::app.type.item.choice.@this<Precision> precision)
        => Wrap(() => DoPower(this, exponent, overflow, precision));

    // A pure numeric op returns its VALUE and THROWS a keyed AppException on error. It has no
    // context, so it cannot born an error Data — the context-ful boundary (the math.* handler /
    // the action dispatch, which preserves AppException.Key) turns the throw into a native plang
    // Data error. END STATE: math becomes [Code] like signing (see the math module TODO +
    // Documentation/v0.2/todos.md) and number returns Data directly — everything flows through Data.
    private static @this Wrap(System.Func<@this> compute)
    {
        try { return compute(); }
        catch (System.DivideByZeroException ex) { throw new global::app.error.AppException(ex.Message, ex, "DivideByZero", 400); }
        catch (PrecisionMixException ex) { throw new global::app.error.AppException(ex.Message, ex, "PrecisionMixRequiresChoice", 400); }
        catch (System.OverflowException ex) { throw new global::app.error.AppException(ex.Message, ex, "MathOverflow", 400); }
        catch (PowerExponentTooLargeException ex) { throw new global::app.error.AppException(ex.Message, ex, "PowerExponentTooLarge", 400); }
        catch (System.ArithmeticException ex) { throw new global::app.error.AppException(ex.Message, ex, "ArithmeticError", 400); }
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

    private static @this DoOp(@this a, @this b, ArithOp op, Overflow overflow, Precision precision)
    {
        Category ca = a.Cat, cb = b.Cat;

        // integer ⊕ integer — BigInteger carrier, narrow per overflow setting.
        if (ca == Category.Integer && cb == Category.Integer)
        {
            BigInteger r = BigOp(a, b, op);
            string floor = WiderInteger(a.Kind.Name, b.Kind.Name);
            return overflow == Overflow.Throw ? NarrowStrict(r, floor) : Narrow(r, floor);
        }

        bool aBF = ca == Category.BinaryFloat, bBF = cb == Category.BinaryFloat;
        bool aDec = ca == Category.Decimal, bDec = cb == Category.Decimal;

        // double ⊕ decimal — the precision fork.
        if ((aBF && bDec) || (aDec && bBF))
            return precision switch
            {
                Precision.Double => DoubleOp(a, b, op),
                Precision.Decimal => DecimalOp(a, b, op),
                _ => throw new PrecisionMixException(),
            };

        // any binary float (with integer or another binary float) → double.
        if (aBF || bBF) return DoubleOp(a, b, op);

        // remaining: decimal with integer/decimal → decimal.
        return DecimalOp(a, b, op);
    }

    // Item-typed interfaces — the operands ride as numbers; CLR appears only AT the +/*/% .NET op
    // (the one genuine boundary), never on the signature.
    private static BigInteger BigOp(@this a, @this b, ArithOp op) => op switch
    {
        ArithOp.Add => a.AsBigInteger() + b.AsBigInteger(),
        ArithOp.Sub => a.AsBigInteger() - b.AsBigInteger(),
        ArithOp.Mul => a.AsBigInteger() * b.AsBigInteger(),
        ArithOp.Mod => b.AsBigInteger() == BigInteger.Zero ? throw new System.DivideByZeroException() : a.AsBigInteger() % b.AsBigInteger(),
        _ => throw new System.InvalidOperationException(),
    };

    private static @this DecimalOp(@this a, @this b, ArithOp op) => op switch
    {
        ArithOp.Add => (@this)(a.AsDecimal() + b.AsDecimal()),
        ArithOp.Sub => (@this)(a.AsDecimal() - b.AsDecimal()),
        ArithOp.Mul => (@this)(a.AsDecimal() * b.AsDecimal()),
        ArithOp.Mod => b.AsDecimal() == 0m ? throw new System.DivideByZeroException() : (@this)(a.AsDecimal() % b.AsDecimal()),
        _ => throw new System.InvalidOperationException(),
    };

    private static @this DoubleOp(@this a, @this b, ArithOp op) => op switch
    {
        ArithOp.Add => (@this)(a.AsDouble() + b.AsDouble()),
        ArithOp.Sub => (@this)(a.AsDouble() - b.AsDouble()),
        ArithOp.Mul => (@this)(a.AsDouble() * b.AsDouble()),
        ArithOp.Mod => (@this)(a.AsDouble() % b.AsDouble()),
        _ => throw new System.InvalidOperationException(),
    };

    private static @this DoDivide(@this a, @this b, Precision precision)
    {
        Category ca = a.Cat, cb = b.Cat;
        bool aBF = ca == Category.BinaryFloat, bBF = cb == Category.BinaryFloat;
        bool aDec = ca == Category.Decimal, bDec = cb == Category.Decimal;

        if ((aBF && bDec) || (aDec && bBF))
            return precision switch
            {
                Precision.Double => DivDouble(a, b),
                Precision.Decimal => DivDecimal(a, b),
                _ => throw new PrecisionMixException(),
            };

        if (aBF || bBF) return DivDouble(a, b);

        // integer/integer, integer/decimal, decimal/decimal — division leaves
        // the integer track to decimal (7/2 → 3.5).
        return DivDecimal(a, b);
    }

    private static @this DivDouble(@this a, @this b) => (@this)(a.AsDouble() / b.AsDouble());

    private static @this DivDecimal(@this a, @this b)
    {
        decimal bd = b.AsDecimal();
        if (bd == 0m) throw new System.DivideByZeroException();
        return (@this)(a.AsDecimal() / bd);
    }

    private static @this DoIntDivide(@this a, @this b, Overflow overflow)
    {
        // Truncating division — explicit C# semantics. Integer operands only;
        // result stays integer (narrowed per policy from the wider operand kind).
        if (a.Cat != Category.Integer || b.Cat != Category.Integer)
            throw new System.ArithmeticException("intdiv requires integer operands.");
        BigInteger bb = b.AsBigInteger();
        if (bb == BigInteger.Zero) throw new System.DivideByZeroException();
        BigInteger r = a.AsBigInteger() / bb;
        string floor = WiderInteger(a.Kind.Name, b.Kind.Name);
        return overflow == Overflow.Throw ? NarrowStrict(r, floor) : Narrow(r, floor);
    }

    private static @this DoPower(@this a, @this exp, Overflow overflow, Precision precision)
    {
        // Fractional exponent ⇒ double IEEE math.
        bool exponentIsFractional =
            (exp.Cat == Category.Decimal && exp.AsDecimal() != System.Math.Truncate(exp.AsDecimal()))
            || (exp.Cat == Category.BinaryFloat && exp.AsDouble() != System.Math.Truncate(exp.AsDouble()));
        if (exponentIsFractional)
            return (@this)(System.Math.Pow(a.AsDouble(), exp.AsDouble()));

        long expL = exp.ToInt64();

        if (expL < 0)
        {
            if (precision == Precision.Decimal && a.Cat != Category.BinaryFloat)
            {
                EnsureExponentInRange(expL);
                decimal acc = 1m, baseD = a.AsDecimal();
                for (long i = 0; i < -expL; i++) acc /= baseD;
                return (@this)(acc);
            }
            return (@this)(System.Math.Pow(a.AsDouble(), expL));
        }

        // Non-negative integer exponent on an integer base — BigInteger carrier, narrow per the overflow setting.
        if (a.Cat == Category.Integer)
        {
            EnsureExponentInRange(expL);
            BigInteger r = BigInteger.Pow(a.AsBigInteger(), (int)expL);
            string floor = a.Kind.Name;
            return overflow == Overflow.Throw ? NarrowStrict(r, floor) : Narrow(r, floor);
        }

        // Decimal base, non-negative integer exponent — repeated decimal multiply.
        if (a.Cat == Category.Decimal)
        {
            EnsureExponentInRange(expL);
            decimal acc = 1m, baseD = a.AsDecimal();
            for (long i = 0; i < expL; i++) acc *= baseD;
            return (@this)(acc);
        }

        // Binary-float base — Math.Pow is constant-time.
        return (@this)(System.Math.Pow(a.AsDouble(), expL));
    }

    private static void EnsureExponentInRange(long expL)
    {
        if (expL > MaxPowerExponent || expL < -MaxPowerExponent)
            throw new PowerExponentTooLargeException(
                $"Integer-exponent magnitude {expL} exceeds limit {MaxPowerExponent}.");
    }
}
