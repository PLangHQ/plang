using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using app.goal;
using app.variable;
using app.module.llm;
using app.module.llm.code;

namespace PLang.Tests.App.Modules.llm;

/// <summary>
/// Snapshot helper for LLM integration tests.
/// Saves real API responses to disk. On subsequent runs, reuses the snapshot
/// if the input messages AND class structure haven't changed.
/// Supports multi-turn snapshots (tool call loops produce multiple HTTP responses).
/// </summary>
internal static class LlmSnapshotHelper
{
    private static readonly string SnapshotDir = System.IO.Path.Combine(
        AppContext.BaseDirectory, "..", "..", "..", "..", "PLang.Tests",
        "App", "Modules", "llm", "snapshots");

    /// <summary>
    /// Types whose shape is included in the cache key.
    /// If any property is added/removed/renamed/retyped, all snapshots invalidate.
    /// </summary>
    private static readonly System.Type[] TrackedTypes = new[]
    {
        typeof(LlmMessage),
        typeof(ToolCall),
        typeof(GoalCall),
        typeof(query),
        typeof(ILlm),
        typeof(OpenAi)
    };

    /// <summary>
    /// Returns a cached response if the snapshot is still valid, otherwise null.
    /// </summary>
    internal static string? TryLoadSnapshot(string testName, List<LlmMessage> messages)
    {
        var key = ComputeKey(testName, messages);
        var path = System.IO.Path.Combine(SnapshotDir, $"{key}.json");

        if (System.IO.File.Exists(path))
            return System.IO.File.ReadAllText(path);

        return null;
    }

    /// <summary>
    /// Returns a multi-turn snapshot as a list of response JSONs, or null.
    /// </summary>
    internal static List<string>? TryLoadMultiSnapshot(string testName, List<LlmMessage> messages)
    {
        var key = ComputeKey(testName, messages);
        var path = System.IO.Path.Combine(SnapshotDir, $"{key}.multi.json");

        if (!System.IO.File.Exists(path)) return null;

        var json = System.IO.File.ReadAllText(path);
        return JsonSerializer.Deserialize<List<string>>(json);
    }

    /// <summary>
    /// Saves a single response snapshot to disk.
    /// </summary>
    internal static void SaveSnapshot(string testName, List<LlmMessage> messages, string responseJson)
    {
        var key = ComputeKey(testName, messages);
        System.IO.Directory.CreateDirectory(SnapshotDir);
        var path = System.IO.Path.Combine(SnapshotDir, $"{key}.json");
        System.IO.File.WriteAllText(path, responseJson);
    }

    /// <summary>
    /// Saves a multi-turn snapshot (list of responses) to disk.
    /// </summary>
    internal static void SaveMultiSnapshot(string testName, List<LlmMessage> messages, List<string> responses)
    {
        var key = ComputeKey(testName, messages);
        System.IO.Directory.CreateDirectory(SnapshotDir);
        var path = System.IO.Path.Combine(SnapshotDir, $"{key}.multi.json");
        System.IO.File.WriteAllText(path, JsonSerializer.Serialize(responses));
    }

    /// <summary>
    /// Computes a cache key from test name + message content + class structure hash.
    /// </summary>
    private static string ComputeKey(string testName, List<LlmMessage> messages)
    {
        var sb = new StringBuilder();
        sb.Append(testName).Append('|');

        foreach (var msg in messages)
        {
            sb.Append(msg.Role).Append(':').Append(msg.Content ?? "").Append('|');
            if (msg.Images != null)
                foreach (var img in msg.Images)
                    sb.Append("img:").Append(img).Append('|');
        }

        sb.Append("struct:").Append(ComputeStructureHash());

        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(sb.ToString()));
        return Convert.ToHexString(bytes).ToLowerInvariant()[..32];
    }

    /// <summary>
    /// Hashes the public property signatures of all tracked types.
    /// </summary>
    private static string ComputeStructureHash()
    {
        var sb = new StringBuilder();
        foreach (var type in TrackedTypes)
        {
            sb.Append(type.FullName).Append('{');

            if (type.IsInterface)
            {
                foreach (var method in type.GetMethods().OrderBy(m => m.Name))
                    sb.Append(method.Name).Append(':').Append(method.ReturnType.Name).Append(',');
            }
            else
            {
                foreach (var prop in type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                    .OrderBy(p => p.Name))
                {
                    sb.Append(prop.Name).Append(':').Append(prop.PropertyType.Name).Append(',');
                }
            }
            sb.Append('}');
        }

        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(sb.ToString()));
        return Convert.ToHexString(bytes).ToLowerInvariant()[..16];
    }
}
