using app.Utils;

namespace PLang.Tests.App.Utility;

// Pin the TypeMismatch error message format. The headline must surface BOTH the
// target FullName (so OBP `@this` types don't render as a useless "this") AND a
// value preview (so unsubstituted `%var%` refs are obvious from the message alone,
// without anyone having to crack open FixSuggestion or attach a debugger).
//
// Tests target non-primitive types (Stream, OBP @this) because the primitive
// conversion path (Convert.ChangeType) has its own dedicated error message — these
// tests exercise the last-resort `targetType.IsAssignableFrom(sourceType) == false`
// branch where the readable message matters most.
public class TypeMismatchMessageTests
{
    [Test]
    public async Task Message_IncludesFullNameOfTarget_NotJustName()
    {
        var (_, error) = TypeConverter.TryConvertTo("hello", typeof(System.IO.Stream));
        await Assert.That(error).IsNotNull();
        await Assert.That(error!.Message).Contains("System.IO.Stream");
        await Assert.That(error.Key).IsEqualTo("TypeMismatch");
    }

    [Test]
    public async Task Message_IncludesValuePreview_ForString()
    {
        // A string source value (e.g. `"%stepResult.actions%"`) is the highest-signal
        // case — the value itself reveals an unresolved %var%. Must be in the headline.
        var (_, error) = TypeConverter.TryConvertTo("%stepResult.actions%", typeof(System.IO.Stream));
        await Assert.That(error).IsNotNull();
        await Assert.That(error!.Message).Contains("%stepResult.actions%");
    }

    [Test]
    public async Task Message_TruncatesLongStrings_ToHundredChars()
    {
        var longStr = new string('x', 500);
        var (_, error) = TypeConverter.TryConvertTo(longStr, typeof(System.IO.Stream));
        await Assert.That(error!.Message).Contains("500 chars");
        await Assert.That(error.Message.Length).IsLessThan(400);
    }

    [Test]
    public async Task Message_OBPThisType_ResolvesViaFullName()
    {
        // The motivating case: `App.Goals.Goal.Steps.Step.Actions.@this` has Name=="this",
        // useless. FullName disambiguates which `@this` it actually is.
        var (_, error) = TypeConverter.TryConvertTo(42L,
            typeof(global::app.goal.steps.step.actions.@this));
        await Assert.That(error!.Message).Contains("app.goal.steps.step.actions");
    }

    [Test]
    public async Task Hint_FlagsUnresolvedVariable_WhenValueContainsPercent()
    {
        var (_, error) = TypeConverter.TryConvertTo("%missing%", typeof(System.IO.Stream));
        await Assert.That(error!.FixSuggestion).Contains("%var%");
    }

    [Test]
    public async Task Hint_GivesTypeNames_WhenValueIsNotAVarReference()
    {
        var (_, error) = TypeConverter.TryConvertTo(42L, typeof(System.IO.Stream));
        await Assert.That(error!.FixSuggestion).Contains("System.Int64");
        await Assert.That(error.FixSuggestion).Contains("System.IO.Stream");
    }

    [Test]
    public async Task PrimitiveBindFailure_NamesParameter_AndNeverLeaksIConvertible()
    {
        // Binding a non-IConvertible object into a string slot used to surface
        // the raw "Object must implement IConvertible". It must instead name the
        // parameter and the expected type. (An Error VALUE is special-cased: the
        // original error propagates, chained — see error.throw F1.)
        var (_, error) = TypeConverter.TryConvertTo(new object(), typeof(string), context: null, targetName: "Message");
        await Assert.That(error).IsNotNull();
        await Assert.That(error!.Message).DoesNotContain("IConvertible");
        await Assert.That(error.Message).Contains("Message");
        await Assert.That(error.Message).Contains("text");
    }
}
