using app.module.condition;
using app.module.condition.code;

namespace PLang.Tests.App.Modules.condition;

// Temporal comparison operators (<,>,<=,>=) through the user-facing
// DefaultEvaluator/Operator path for datetime/date/time/duration. Previously
// covered only at the low-level Stage4_PerTypeCompareTests, never through Operator.
public class TemporalOperatorTests
{
    private readonly Default _eval = new();
    private static Data D(object? value) => value == null ? new Data("") : global::PLang.Tests.TestApp.SharedContext.Ok(value);

    private Task<global::app.data.@this<global::app.type.item.@bool.@this>> Eval(object? left, string op, object? right)
        => _eval.Evaluate(new Compare(global::PLang.Tests.TestApp.SharedContext) { Left = D(left), Operator = global::PLang.Tests.TestApp.SharedContext.Ok<global::app.type.item.choice.@this<Operator>>((global::app.type.item.choice.@this<Operator>)new Operator(op)), Right = D(right) });

    private bool IsTrue(global::app.data.@this<global::app.type.item.@bool.@this> r) => r.Success && (r.Peek() as global::app.type.item.@bool.@this)?.Value == true;
    private bool IsFalse(global::app.data.@this<global::app.type.item.@bool.@this> r) => r.Success && (r.Peek() as global::app.type.item.@bool.@this)?.Value == false;

    private static global::app.type.item.datetime.@this Dt(string iso) => new(System.DateTimeOffset.Parse(iso));
    private static global::app.type.item.date.@this Date(string iso) => new(System.DateOnly.Parse(iso));
    private static global::app.type.item.time.@this Time(string iso) => new(System.TimeOnly.Parse(iso));
    private static global::app.type.item.duration.@this Dur(System.TimeSpan ts) => new(ts);

    [Test] public async Task Datetime_LessThan() => await Assert.That(IsTrue(await Eval(Dt("2024-01-01T00:00:00Z"), "<", Dt("2024-06-01T00:00:00Z")))).IsTrue();
    [Test] public async Task Datetime_GreaterThan() => await Assert.That(IsTrue(await Eval(Dt("2024-06-01T00:00:00Z"), ">", Dt("2024-01-01T00:00:00Z")))).IsTrue();
    [Test] public async Task Datetime_GreaterOrEqual_Equal() => await Assert.That(IsTrue(await Eval(Dt("2024-06-01T00:00:00Z"), ">=", Dt("2024-06-01T00:00:00Z")))).IsTrue();
    [Test] public async Task Datetime_LessOrEqual_False() => await Assert.That(IsFalse(await Eval(Dt("2024-06-01T00:00:00Z"), "<=", Dt("2024-01-01T00:00:00Z")))).IsTrue();

    [Test] public async Task Date_LessThan() => await Assert.That(IsTrue(await Eval(Date("2024-01-01"), "<", Date("2024-06-01")))).IsTrue();
    [Test] public async Task Date_GreaterThan() => await Assert.That(IsTrue(await Eval(Date("2024-06-01"), ">", Date("2024-01-01")))).IsTrue();

    [Test] public async Task Time_LessThan() => await Assert.That(IsTrue(await Eval(Time("08:00"), "<", Time("17:30")))).IsTrue();
    [Test] public async Task Time_GreaterOrEqual() => await Assert.That(IsTrue(await Eval(Time("17:30"), ">=", Time("08:00")))).IsTrue();

    [Test] public async Task Duration_LessThan() => await Assert.That(IsTrue(await Eval(Dur(System.TimeSpan.FromSeconds(30)), "<", Dur(System.TimeSpan.FromMinutes(2))))).IsTrue();
    [Test] public async Task Duration_GreaterThan() => await Assert.That(IsTrue(await Eval(Dur(System.TimeSpan.FromMinutes(2)), ">", Dur(System.TimeSpan.FromSeconds(30))))).IsTrue();
}
