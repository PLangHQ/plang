using System.IO;
using Fluid;
using Fluid.Ast;
using Fluid.Values;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Primitives;
using App.FileSystem;
using App.FileSystem.Default;
using App.Errors;
using App.FileSystem;
using App.Goals.Goal;
using App.Variables;

namespace App.modules.ui.providers;

public class FluidProvider : ITemplateProvider
{
    public string Name => "default";
    public bool IsDefault { get; set; }

    private FluidParser CreateParser()
    {
        var parser = new FluidParser();
        parser.RegisterExpressionTag("callGoal", CallGoalTagAsync);
        return parser;
    }

    public async Task<Data> Render(Render action)
    {
        var templateContent = action.Template;
        var isFile = action.IsFile;
        string? sourceFile = null;

        // Resolve template content: file or inline
        if (isFile == true || (isFile == null && LooksLikeFilePath(templateContent)))
        {
            var pathData = FileSystem.Path.Resolve(templateContent, action.Context);
            if (!pathData.Exists)
                return Data.FromError(new ServiceError(
                    $"Template file not found: {templateContent}", "NotFound", 404));

            sourceFile = pathData.Relative;
            try
            {
                var fs = action.Context.App.FileSystem;
                templateContent = fs.File.ReadAllText(pathData.Absolute);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                return Data.FromError(new ServiceError(ex.Message, "IOError", 500));
            }
        }

        // Parse
        var parser = CreateParser();
        if (!parser.TryParse(templateContent, out var fluidTemplate, out var parseError))
        {
            var location = sourceFile != null ? $" in '{sourceFile}'" : "";
            return Data.FromError(new ServiceError(
                $"Template syntax error{location}: {parseError}", "TemplateError", 400));
        }

        // Build context
        var options = new TemplateOptions();
        options.MaxSteps = 100_000; // Defense-in-depth: prevent pathological templates from running indefinitely
        options.MaxRecursion = 100; // Prevent deeply recursive includes
        // UnsafeMemberAccessStrategy allows access to all public properties without per-type registration.
        // PLang templates render internal objects (Goals, Steps, Actions) — no user-supplied types.
        options.MemberAccessStrategy = new UnsafeMemberAccessStrategy();
        options.MemberAccessStrategy.IgnoreCasing = true;
        options.MemberAccessStrategy.MemberNameStrategy = MemberNameStrategies.Default;

        // Configure file provider for {% include %} / {% render %} tags
        var fs2 = action.Context.App.FileSystem;
        var basePath = GetTemplateBaseDir(action);
        options.FileProvider = new PlangFileProvider(fs2, basePath);

        var fluidContext = new TemplateContext(options);

        // Store app + Context.@this for callGoal tag access
        fluidContext.AmbientValues["engine"] = action.Context.App;
        fluidContext.AmbientValues["context"] = action.Context;

        // Load Variables (GetAll already excludes !-prefixed)
        foreach (var data in action.Context.Variables.GetAll())
        {
            fluidContext.SetValue(data.Name, FluidValue.Create(data.Value, options));
        }

        // Override with explicit parameters
        if (action.Parameters != null)
        {
            foreach (var param in action.Parameters)
            {
                fluidContext.SetValue(param.Name, FluidValue.Create(param.Value, options));
            }
        }

        // Render with HTML encoding for security (XSS prevention)
        try
        {
            var writer = new StringWriter();
            await fluidTemplate.RenderAsync(writer, NullEncoder.Default, fluidContext);
            return Data.Ok(writer.ToString());
        }
        catch (Exception ex) when (ex is not (NullReferenceException or OutOfMemoryException or StackOverflowException))
        {
            var location = sourceFile != null ? $" in '{sourceFile}'" : "";
            return Data.FromError(new ServiceError(
                $"Template render error{location}: {ex.Message}", "RenderError", 500));
        }
    }

    /// <summary>
    /// Heuristic: a string looks like a file path if it contains a dot-extension
    /// and no Liquid syntax markers. Used only when IsFile is null (auto-detect).
    /// </summary>
    private static bool LooksLikeFilePath(string template)
    {
        if (string.IsNullOrWhiteSpace(template)) return false;
        // If it contains Liquid delimiters, it's inline content
        if (template.Contains("{{") || template.Contains("{%")) return false;
        // If it has a file extension pattern, likely a path
        var lastDot = template.LastIndexOf('.');
        if (lastDot <= 0) return false;
        var ext = template[lastDot..];
        return ext.Length >= 2 && ext.Length <= 6 && !ext.Contains(' ');
    }

    /// <summary>
    /// Gets the base directory for template file resolution (includes).
    /// Resolves from the calling goal's directory, or app root as fallback.
    /// </summary>
    private static string GetTemplateBaseDir(Render action)
    {
        var engine = action.Context.App;
        var goalPath = action.Context.Goal?.Path;
        if (!string.IsNullOrEmpty(goalPath))
        {
            var fs = engine.FileSystem;
            var goalDir = fs.Path.GetDirectoryName(goalPath);
            if (!string.IsNullOrEmpty(goalDir))
                return fs.ValidatePath(goalDir);
        }
        return engine.AbsolutePath;
    }

    /// <summary>
    /// Custom Fluid tag handler for {% callGoal 'GoalName' %} or {% callGoal GoalName %}.
    /// Invokes a PLang goal and writes the result into template output.
    /// </summary>
    private static async ValueTask<Completion> CallGoalTagAsync(
        Fluid.Ast.Expression expression, TextWriter writer, System.Text.Encodings.Web.TextEncoder encoder,
        TemplateContext context)
    {
        var engine = (App.@this)context.AmbientValues["engine"];
        var plangContext = (Context.@this)context.AmbientValues["context"];

        try
        {
            var goalNameValue = await expression.EvaluateAsync(context);
            var goalName = goalNameValue?.ToStringValue() ?? "";
            if (string.IsNullOrEmpty(goalName))
            {
                await writer.WriteAsync("[Error: callGoal requires a goal name]");
                return Completion.Normal;
            }

            var goalCall = new GoalCall { Name = goalName };
            var result = await engine.RunGoalAsync(goalCall, plangContext);

            if (result.Success)
            {
                var output = result.Value?.ToString() ?? "";
                await writer.WriteAsync(output);
            }
            else
            {
                // Show error message in template output per Ingi's requirement
                var errorMessage = result.Error?.Message ?? "Unknown error";
                await writer.WriteAsync($"[Error: {errorMessage}]");
            }
        }
        catch (Exception ex) when (ex is not (NullReferenceException or OutOfMemoryException or StackOverflowException))
        {
            await writer.WriteAsync($"[Error: {ex.Message}]");
        }

        return Completion.Normal;
    }

    // --- Microsoft.Extensions.FileProviders adapter for Fluid's include/render tags ---

    private sealed class PlangFileProvider : IFileProvider
    {
        private readonly IPLangFileSystem _fs;
        private readonly string _basePath;

        public PlangFileProvider(IPLangFileSystem fs, string basePath)
        {
            _fs = fs;
            _basePath = basePath;
        }

        public IFileInfo GetFileInfo(string subpath)
        {
            // Fluid appends ".liquid" to include paths — try both with and without
            var candidates = new[] { subpath, StripLiquidExtension(subpath) };
            foreach (var candidate in candidates)
            {
                if (string.IsNullOrEmpty(candidate)) continue;
                var fullPath = TryResolvePath(candidate);
                if (fullPath != null && _fs.File.Exists(fullPath))
                    return new PlangFileInfo(_fs, fullPath, candidate);
            }
            return new NotFoundFileInfo(subpath);
        }

        private string? TryResolvePath(string candidate)
        {
            try
            {
                return _fs.ValidatePath(_fs.Path.Combine(_basePath, candidate));
            }
            catch (Exception ex) when (ex is not (NullReferenceException or OutOfMemoryException or StackOverflowException))
            {
                // Path validation failed (sandbox violation, invalid path, etc.)
                // Return null → GetFileInfo returns NotFoundFileInfo → Fluid reports "file not found"
                System.Diagnostics.Debug.WriteLine($"Template include path validation failed for '{candidate}': {ex.Message}");
                return null;
            }
        }

        private static string StripLiquidExtension(string path)
        {
            const string ext = ".liquid";
            return path.EndsWith(ext, StringComparison.OrdinalIgnoreCase)
                ? path[..^ext.Length]
                : path;
        }

        public IDirectoryContents GetDirectoryContents(string subpath)
            => new NotFoundDirectoryContents();

        public IChangeToken Watch(string filter)
            => NullChangeToken.Singleton;
    }

    private sealed class PlangFileInfo : IFileInfo
    {
        private readonly IPLangFileSystem _fs;
        private readonly string _fullPath;

        public PlangFileInfo(IPLangFileSystem fs, string fullPath, string name)
        {
            _fs = fs;
            _fullPath = fullPath;
            Name = name;
        }

        public bool Exists => true;
        public long Length => _fs.FileInfo.New(_fullPath).Length;
        public string? PhysicalPath => _fullPath;
        public string Name { get; }
        public DateTimeOffset LastModified => DateTimeOffset.UtcNow;
        public bool IsDirectory => false;

        public Stream CreateReadStream()
        {
            var content = _fs.File.ReadAllText(_fullPath);
            return new MemoryStream(System.Text.Encoding.UTF8.GetBytes(content));
        }
    }
}
