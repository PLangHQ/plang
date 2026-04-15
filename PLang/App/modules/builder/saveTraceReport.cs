using System.Text.Json;
using System.Text.Json.Nodes;

namespace App.modules.builder;

/// <summary>
/// Reads all trace JSON files from .build/traces/ and assembles build-run.json
/// for the trace web viewer.
/// </summary>
[Action("saveTraceReport")]
public partial class saveTraceReport : IContext
{
    public Task<Data.@this> Run()
    {
        var fs = Context.App!.FileSystem;
        var tracesDir = fs.Path.Combine(Context.App.AbsolutePath, ".build", "traces");

        if (!fs.Directory.Exists(tracesDir))
            return Task.FromResult(Data.@this.Ok(false));

        var traceFiles = fs.Directory.GetFiles(tracesDir, "*.json")
            .Where(f => !f.EndsWith("manifest.json") && !f.EndsWith("build-run.json"))
            .OrderBy(f => f)
            .ToArray();

        var goals = new JsonArray();
        foreach (var file in traceFiles)
        {
            try
            {
                var json = fs.File.ReadAllText(file);
                var doc = JsonNode.Parse(json);
                if (doc == null) continue;

                var pass1 = doc["pass1"];
                var response = pass1?["response"];
                var steps = response is JsonObject respObj ? respObj["steps"]?.AsArray() : null;

                var goalSteps = new JsonArray();
                if (steps != null)
                {
                    foreach (var s in steps)
                    {
                        if (s == null) continue;
                        var actions = s["actions"]?.DeepClone() ?? new JsonArray();
                        var level = s["level"]?.GetValue<string>() ?? "high";
                        var confidence = s["confidence"]?.GetValue<int>() ?? 0;

                        var flow = new JsonArray
                        {
                            new JsonObject
                            {
                                ["phase"] = "buildGoal",
                                ["level"] = level,
                                ["confidence"] = confidence,
                                ["guidance"] = s["guidance"]?.GetValue<string>() ?? ""
                            },
                            new JsonObject
                            {
                                ["phase"] = "validate",
                                ["status"] = actions.Count > 0 ? "pass" : "error"
                            },
                            new JsonObject
                            {
                                ["phase"] = "applyStep",
                                ["status"] = "merged"
                            }
                        };

                        goalSteps.Add(new JsonObject
                        {
                            ["index"] = s["index"]?.DeepClone(),
                            ["text"] = s["guidance"]?.GetValue<string>() ?? "",
                            ["finalLevel"] = level,
                            ["finalConfidence"] = confidence,
                            ["actions"] = actions,
                            ["flow"] = flow
                        });
                    }
                }

                goals.Add(new JsonObject
                {
                    ["name"] = doc["goal"]?.GetValue<string>() ?? "?",
                    ["path"] = "",
                    ["status"] = "ok",
                    ["timestamp"] = doc["timestamp"]?.GetValue<string>() ?? "",
                    ["stepCount"] = goalSteps.Count,
                    ["buildGoal"] = new JsonObject
                    {
                        ["system"] = pass1?["system"]?.GetValue<string>() ?? "",
                        ["user"] = pass1?["user"]?.GetValue<string>() ?? "",
                        ["response"] = response?.ToJsonString() ?? ""
                    },
                    ["steps"] = goalSteps
                });
            }
            catch
            {
                // Skip malformed trace files
            }
        }

        var buildRun = new JsonObject
        {
            ["buildRunId"] = "build",
            ["timestamp"] = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ"),
            ["path"] = ".",
            ["goalCount"] = goals.Count,
            ["goals"] = goals
        };

        var outPath = fs.Path.Combine(tracesDir, "build-run.json");
        fs.File.WriteAllText(outPath, buildRun.ToJsonString(new JsonSerializerOptions { WriteIndented = false }));

        return Task.FromResult(Data.@this.Ok(true));
    }
}
