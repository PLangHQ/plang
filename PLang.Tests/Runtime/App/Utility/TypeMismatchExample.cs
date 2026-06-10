using app.Utils;

namespace PLang.Tests.App.Utility;

// Tiny fixture that prints the actual TypeMismatch message format the user will
// see when an unresolved %var% slips into a TypeConverter call. Used to eyeball
// the readable shape of the error.
public class TypeMismatchExampleSnapshot
{
    [Test]
    public async Task Snapshot_ExampleErrorOutput()
    {
        var (_, err) = TypeConverter.TryConvertTo(
            "%stepResult.actions%",
            typeof(global::app.goal.steps.step.actions.@this));

        await Assert.That(err).IsNotNull();
        // The C# identifier `@this` renders in Type.FullName as just `this` (the `@`
        // is a source-only escape for using a keyword as an identifier).
        await Assert.That(err!.Message).Contains("app.goal.steps.step.actions.this");
        await Assert.That(err.Message).Contains("%stepResult.actions%");
        await Assert.That(err.FixSuggestion).Contains("%var%");

        // Print so a human running this test once with --output:detailed sees
        // the actual rendered message.
        System.Console.WriteLine("─── TypeMismatch sample ───");
        System.Console.WriteLine("MESSAGE: " + err.Message);
        System.Console.WriteLine("HINT:    " + err.FixSuggestion);
        System.Console.WriteLine("───────────────────────────");
    }
}
