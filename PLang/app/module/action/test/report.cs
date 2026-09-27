namespace app.module.action.test;

/// <summary>
/// Writes the run out — the run's report is the app's test list's (<c>app.test.list.Report.Write</c>): the
/// console (unless the run is nested in a test) and one artefact at the app root, <c>.test/results.json</c>
/// or <c>.test/junit.xml</c> by format. Returns the tests; a top-level run that didn't pass is its error.
/// </summary>
[Action("report", Cacheable = false)]
public partial class report : IContext
{
    public partial data.@this<global::app.type.item.text.@this>? Format { get; init; }

    public async Task<data.@this> Start()
        => await Context.App.test.list.Report.Write(Format == null ? null : await Format.Value(), Context);
}
