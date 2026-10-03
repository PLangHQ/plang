using System.Security;
using System.Text;

namespace app.test.junit;

/// <summary>
/// JUnit XML — a kind of test: a run's tests written as the document CI tools read (not the plang wire), so
/// the kind owns its own rendering. Grouped by the goal's parent folder as testsuites. A run's report in
/// junit is <c>.test/junit.xml</c>.
/// </summary>
public sealed class @this : global::app.type.kind.@this
{
    public @this() : base("junit") { }

    protected internal override string Owner => "test";

    public override System.Collections.Generic.IReadOnlyList<string> Mime => ["application/junit+xml"];

    public override System.Collections.Generic.IReadOnlyList<string> Extension => [];

    public override bool IsText => true;

    public override string? Report => "junit.xml";

    /// <summary>The run <paramref name="data"/> holds (its tests), written as a JUnit document.</summary>
    public override async System.Threading.Tasks.Task<global::app.data.@this> Encode(System.IO.Stream stream,
        global::app.data.@this data, global::app.actor.context.@this context, global::app.View? view = null,
        System.Text.Encoding? encoding = null, System.Threading.CancellationToken ct = default)
    {
        if (data.Peek() is not global::app.type.item.list.@this<global::app.test.@this> run)
            return context.Error(new global::app.error.Error(
                $"%{data.Name}% holds no test run to write as junit", "NothingToWrite", 400));
        var bytes = (encoding ?? System.Text.Encoding.UTF8).GetBytes(Document(run.Items().ToList()));
        await stream.WriteAsync(bytes, ct);
        return context.Ok();
    }

    private string Document(IReadOnlyList<global::app.test.@this> tests)
    {
        var sb = new StringBuilder();
        sb.AppendLine("<?xml version=\"1.0\" encoding=\"UTF-8\"?>");
        sb.AppendLine($"<testsuites tests=\"{tests.Count}\" failures=\"{tests.Count(t => t.Status == Status.Fail)}\" errors=\"{tests.Count(t => t.Status == Status.Stale)}\">");
        // Group by the goal's parent folder (path verb, no string surgery).
        var byPath = tests.GroupBy(t => t.Goal.Path?.Parent?.ToString() ?? "");
        foreach (var group in byPath)
        {
            var suite = group.ToList();
            var failures = suite.Count(t => t.Status == Status.Fail);
            var errors = suite.Count(t => t.Status == Status.Stale);
            var timeSec = suite.Sum(t => t.Duration.Value.TotalSeconds);
            sb.AppendLine($"  <testsuite name=\"{SecurityElement.Escape(group.Key)}\" tests=\"{suite.Count}\" failures=\"{failures}\" errors=\"{errors}\" time=\"{timeSec:F3}\">");
            foreach (var test in suite)
            {
                var name = SecurityElement.Escape(test.Goal.Path?.ToString() ?? "") ?? "";
                sb.Append($"    <testcase name=\"{name}\" time=\"{test.Duration.Value.TotalSeconds:F3}\"");
                if (test.Status == Status.Pass) sb.AppendLine(" />");
                else
                {
                    sb.AppendLine(">");
                    switch (test.Status)
                    {
                        case Status.Fail:
                            sb.AppendLine($"      <failure>{SecurityElement.Escape(test.Error?.Message ?? "fail")}</failure>");
                            break;
                        case Status.Timeout:
                            sb.AppendLine($"      <failure type=\"timeout\">timeout</failure>");
                            break;
                        // A test that could not load is an error, never quietly skipped.
                        case Status.Stale:
                            sb.AppendLine($"      <error message=\"could not load\">{SecurityElement.Escape(test.StatusReason?.Clr<string>() ?? "stale")}</error>");
                            break;
                        case Status.Skipped:
                            sb.AppendLine($"      <skipped>{SecurityElement.Escape(test.StatusReason?.Clr<string>() ?? test.Status.ToString())}</skipped>");
                            break;
                    }
                    sb.AppendLine("    </testcase>");
                }
            }
            sb.AppendLine("  </testsuite>");
        }
        sb.AppendLine("</testsuites>");
        return sb.ToString();
    }
}
