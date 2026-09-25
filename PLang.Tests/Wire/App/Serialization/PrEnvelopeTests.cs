namespace PLang.Tests.App.Serialization;

// The .pr a goal writes: each golden goal (formal_golden.json's .pr rows) read as a .pr, written, read and
// written again — the two writes are byte-equal, and each step's actions ride under `code`.
public class PrEnvelopeTests : System.IAsyncDisposable
{
    private readonly global::app.@this _app = TestApp.Create(
        System.IO.Path.Combine(System.IO.Path.GetTempPath(), "prenvelope-" + System.Guid.NewGuid().ToString("N")[..8]));

    public async System.Threading.Tasks.ValueTask DisposeAsync() => await _app.DisposeAsync();

    [Test]
    public async Task EveryGoldenGoal_WrittenAsPr_ReadsBackByteEqual()
    {
        var differ = new List<string>();
        var goals = FormalWriterTests.Golden().GroupBy(e => e.GetProperty("goal").GetString()!).ToList();
        foreach (var goal in goals)
        {
            var steps = goal.Select(e => "{\"index\":" + e.GetProperty("index").GetInt32()
                + ",\"text\":" + System.Text.Json.JsonSerializer.Serialize(e.GetProperty("text").GetString())
                + ",\"lineNumber\":" + (e.GetProperty("index").GetInt32() + 2)
                + ",\"code\":" + e.GetProperty("pr").GetRawText() + "}");
            var pr = "{\"name\":\"" + goal.Key + "\",\"path\":\"/" + goal.Key + ".goal\",\"step\":[" + string.Join(",", steps) + "]}";

            var first = await Write(await RealGoalLoad.Read(_app, pr));
            var second = await Write(await RealGoalLoad.Read(_app, first));

            if (first != second) differ.Add($"{goal.Key}\n  first:  {first}\n  second: {second}");
            if (!first.Contains("\"code\"") || System.Text.RegularExpressions.Regex.IsMatch(first, "\"action\"\\s*:"))
                differ.Add($"{goal.Key} writes no `code`: {first}");
        }
        await Assert.That(goals.Count).IsEqualTo(5);
        await Assert.That(string.Join("\n", differ)).IsEqualTo("");
    }

    private async Task<string> Write(global::app.goal.@this goal)
    {
        var serializer = (global::app.channel.serializer.plang.@this)
            _app.User.Channel.Serializers.GetOrDefault("application/plang");
        using var ms = new System.IO.MemoryStream();
        await serializer.SerializeItemAsync(ms, goal, global::app.View.Store);
        return System.Text.Encoding.UTF8.GetString(ms.ToArray());
    }
}
