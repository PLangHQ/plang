namespace PLang.Tests.App.Testing;

// A test report's format is a format kind that writes a report, by the kind's own answer — never a list of names.
public class ReportFormatTests
{
    [Test] public async Task TheFormats_AreTheKindsThatWriteAReport()
        => await Assert.That(global::app.test.format.@this.Choices(null)).IsEquivalentTo(new[] { "json", "junit" });

    // text is a format kind, but it writes no report: it isn't offered, and naming it is refused.
    [Test] public async Task AKindThatWritesNoReport_IsNotOffered()
    {
        await Assert.That(global::app.test.format.@this.Choices(null)).DoesNotContain("text");
        await Assert.That(() => new global::app.test.format.@this("text")).Throws<ArgumentException>();
    }

    [Test] public async Task SettingAFormatThatWritesNoReport_IsRefused()
    {
        await using var app = new global::app.@this("/app").Testing();

        var set = app.actor.list.System.Setting.Set("app.test.setting", new Dictionary<string, object?> { ["format"] = "text" });

        await Assert.That(set.Success).IsFalse();
    }
}
