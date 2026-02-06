using System.Text.Json;
using PLang.Runtime2.Core;
using PLang.Runtime2.Errors;

namespace PLang.Runtime2.Modules;

/// <summary>
/// File operations module for Runtime2.
/// </summary>
public class FileModule : BaseModule
{
    public override string Name => "file";

    public override IEnumerable<string> Aliases => new[] { "io", "fs" };

    public override IEnumerable<string> GetMethods() => new[]
    {
        "save", "read", "delete", "exists", "copy", "move"
    };

    public override async Task<GoalResult> ExecuteAsync(string method, object? parameters)
    {
        return method.ToLowerInvariant() switch
        {
            "save" => await Save(parameters),
            "read" => await Read(parameters),
            "delete" => await Delete(parameters),
            "exists" => await Exists(parameters),
            "copy" => await Copy(parameters),
            "move" => await Move(parameters),
            _ => Error($"Unknown method: {method}")
        };
    }

    /// <summary>
    /// Save an object to a file as JSON.
    /// </summary>
    public async Task<GoalResult> Save(object? parameters)
    {
        if (parameters == null)
            return Error("Parameters required for save");

        string? path = null;
        object? content = null;

        // Extract path and content from parameters
        if (parameters is IDictionary<string, object?> dict)
        {
            path = dict.TryGetValue("path", out var p) ? p?.ToString() : null;
            content = dict.TryGetValue("content", out var c) ? c : null;

            // Also check for 'data' as alternative to 'content'
            if (content == null)
                content = dict.TryGetValue("data", out var d) ? d : null;
        }

        if (string.IsNullOrEmpty(path))
            return Error("Path is required");

        if (content == null)
            return Error("Content is required");

        try
        {
            var absolutePath = GetAbsolutePath(path);
            var directory = Path.GetDirectoryName(absolutePath);

            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            string json;
            if (content is string strContent)
            {
                json = strContent;
            }
            else
            {
                json = JsonSerializer.Serialize(content, new JsonSerializerOptions
                {
                    WriteIndented = true,
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                    DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
                });
            }

            await File.WriteAllTextAsync(absolutePath, json);

            return Success(new { Path = absolutePath });
        }
        catch (Exception ex)
        {
            return Error($"Failed to save file: {ex.Message}");
        }
    }

    /// <summary>
    /// Save content to a specific path.
    /// </summary>
    public async Task<GoalResult> Save(string path, object content)
    {
        var parameters = new Dictionary<string, object?>
        {
            { "path", path },
            { "content", content }
        };
        return await Save(parameters);
    }

    /// <summary>
    /// Read a file.
    /// </summary>
    public async Task<GoalResult> Read(object? parameters)
    {
        string? path = null;

        if (parameters is string strPath)
        {
            path = strPath;
        }
        else if (parameters is IDictionary<string, object?> dict)
        {
            path = dict.TryGetValue("path", out var p) ? p?.ToString() : null;
        }

        if (string.IsNullOrEmpty(path))
            return Error("Path is required");

        try
        {
            var absolutePath = GetAbsolutePath(path);

            if (!File.Exists(absolutePath))
                return Error($"File not found: {path}");

            var content = await File.ReadAllTextAsync(absolutePath);
            return Success(content);
        }
        catch (Exception ex)
        {
            return Error($"Failed to read file: {ex.Message}");
        }
    }

    /// <summary>
    /// Delete a file.
    /// </summary>
    public async Task<GoalResult> Delete(object? parameters)
    {
        string? path = null;

        if (parameters is string strPath)
        {
            path = strPath;
        }
        else if (parameters is IDictionary<string, object?> dict)
        {
            path = dict.TryGetValue("path", out var p) ? p?.ToString() : null;
        }

        if (string.IsNullOrEmpty(path))
            return Error("Path is required");

        try
        {
            var absolutePath = GetAbsolutePath(path);

            if (File.Exists(absolutePath))
            {
                File.Delete(absolutePath);
            }

            return Success();
        }
        catch (Exception ex)
        {
            return Error($"Failed to delete file: {ex.Message}");
        }
    }

    /// <summary>
    /// Check if a file exists.
    /// </summary>
    public async Task<GoalResult> Exists(object? parameters)
    {
        string? path = null;

        if (parameters is string strPath)
        {
            path = strPath;
        }
        else if (parameters is IDictionary<string, object?> dict)
        {
            path = dict.TryGetValue("path", out var p) ? p?.ToString() : null;
        }

        if (string.IsNullOrEmpty(path))
            return Error("Path is required");

        var absolutePath = GetAbsolutePath(path);
        return Success(File.Exists(absolutePath));
    }

    /// <summary>
    /// Copy a file.
    /// </summary>
    public async Task<GoalResult> Copy(object? parameters)
    {
        if (parameters is not IDictionary<string, object?> dict)
            return Error("Parameters required for copy");

        var source = dict.TryGetValue("source", out var s) ? s?.ToString() : null;
        var destination = dict.TryGetValue("destination", out var d) ? d?.ToString() : null;
        var overwrite = dict.TryGetValue("overwrite", out var o) && o is bool b && b;

        if (string.IsNullOrEmpty(source))
            return Error("Source path is required");
        if (string.IsNullOrEmpty(destination))
            return Error("Destination path is required");

        try
        {
            var absSource = GetAbsolutePath(source);
            var absDest = GetAbsolutePath(destination);

            var destDir = Path.GetDirectoryName(absDest);
            if (!string.IsNullOrEmpty(destDir) && !Directory.Exists(destDir))
            {
                Directory.CreateDirectory(destDir);
            }

            File.Copy(absSource, absDest, overwrite);
            return Success(new { Source = absSource, Destination = absDest });
        }
        catch (Exception ex)
        {
            return Error($"Failed to copy file: {ex.Message}");
        }
    }

    /// <summary>
    /// Move a file.
    /// </summary>
    public async Task<GoalResult> Move(object? parameters)
    {
        if (parameters is not IDictionary<string, object?> dict)
            return Error("Parameters required for move");

        var source = dict.TryGetValue("source", out var s) ? s?.ToString() : null;
        var destination = dict.TryGetValue("destination", out var d) ? d?.ToString() : null;

        if (string.IsNullOrEmpty(source))
            return Error("Source path is required");
        if (string.IsNullOrEmpty(destination))
            return Error("Destination path is required");

        try
        {
            var absSource = GetAbsolutePath(source);
            var absDest = GetAbsolutePath(destination);

            var destDir = Path.GetDirectoryName(absDest);
            if (!string.IsNullOrEmpty(destDir) && !Directory.Exists(destDir))
            {
                Directory.CreateDirectory(destDir);
            }

            File.Move(absSource, absDest);
            return Success(new { Source = absSource, Destination = absDest });
        }
        catch (Exception ex)
        {
            return Error($"Failed to move file: {ex.Message}");
        }
    }

    private string GetAbsolutePath(string path)
    {
        if (Path.IsPathRooted(path))
            return path;

        // Use current directory as base
        return Path.Combine(Environment.CurrentDirectory, path);
    }
}
