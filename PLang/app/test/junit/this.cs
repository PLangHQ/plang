using System.Security;
using System.Text;

namespace app.test.junit;

/// <summary>
/// The JUnit-XML view of a set of tests — a genuine external CI format (not the
/// plang wire), so the format owns its own rendering. <c>ToString()</c> is the
/// document. Grouped by the goal's parent folder as testsuites.
/// </summary>
public sealed class @this
{
    private readonly IReadOnlyList<global::app.test.@this> _tests;

    public @this(IReadOnlyList<global::app.test.@this> tests) => _tests = tests;

    public override string ToString()
    {
        var sb = new StringBuilder();
        sb.AppendLine("<?xml version=\"1.0\" encoding=\"UTF-8\"?>");
        sb.AppendLine($"<testsuites tests=\"{_tests.Count}\" failures=\"{_tests.Count(t => t.Status == Status.Fail)}\" errors=\"0\">");
        // Group by the goal's parent folder (path verb, no string surgery).
        var byPath = _tests.GroupBy(t => t.Goal.Path?.Parent?.ToString() ?? "");
        foreach (var group in byPath)
        {
            var suite = group.ToList();
            var failures = suite.Count(t => t.Status == Status.Fail);
            var timeSec = suite.Sum(t => t.Duration.TotalSeconds);
            sb.AppendLine($"  <testsuite name=\"{SecurityElement.Escape(group.Key)}\" tests=\"{suite.Count}\" failures=\"{failures}\" time=\"{timeSec:F3}\">");
            foreach (var test in suite)
            {
                var name = SecurityElement.Escape(test.Goal.Path?.ToString() ?? "") ?? "";
                sb.Append($"    <testcase name=\"{name}\" time=\"{test.Duration.TotalSeconds:F3}\"");
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
                        case Status.Stale:
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
