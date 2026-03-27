using System.IO;
using Fluid;
using Fluid.Ast;
using Fluid.Values;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Primitives;
using PLang.Interfaces;
using PLang.Runtime2.Engine.Errors;
using PLangContext = PLang.Runtime2.Engine.Context.PLangContext;
using PLang.Runtime2.Engine.FileSystem;
using PLang.Runtime2.Engine.Goals.Goal;
using PLang.Runtime2.Engine.Memory;

namespace PLang.Runtime2.modules.ui.providers;

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

        // Resolve template content: file or inline
        if (isFile == true || (isFile == null && LooksLikeFilePath(templateContent)))
        {
            var pathData = new PathData(templateContent, action.Context);
            if (!pathData.Exists)
                return Data.FromError(new ServiceError(
                    $"Template file not found: {templateContent}", "NotFound", 404));

            try
            {
                var fs = action.Context.Engine.FileSystem;
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
            return Data.FromError(new ServiceError(
                $"Template syntax error: {parseError}", "TemplateError", 400));

        // Build context
        var options = new TemplateOptions();
        options.MemberAccessStrategy.IgnoreCasing = true;
        options.MemberAccessStrategy.MemberNameStrategy = MemberNameStrategies.Default;

        // Configure file provider for {% include %} / {% render %} tags
        var fs2 = action.Context.Engine.FileSystem;
        var basePath = GetTemplateBaseDir(action);
        options.FileProvider = new PlangFileProvider(fs2, basePath);

        var fluidContext = new TemplateContext(options);

        // Store engine + PLangContext for callGoal tag access
        fluidContext.AmbientValues["engine"] = action.Context.Engine;
        fluidContext.AmbientValues["context"] = action.Context;

        // Load memory stack variables (GetAll already excludes !-prefixed)
        foreach (var data in action.Context.MemoryStack.GetAll())
        {
            var value = data.Value;
            RegisterTypeIfNeeded(options, value);
            fluidContext.SetValue(data.Name, FluidValue.Create(value, options));
        }

        // Override with explicit parameters
        if (action.Parameters != null)
        {
            foreach (var param in action.Parameters)
            {
                RegisterTypeIfNeeded(options, param.Value);
                fluidContext.SetValue(param.Name, FluidValue.Create(param.Value, options));
            }
        }

        // Render with HTML encoding for security (XSS prevention)
        try
        {
            var writer = new StringWriter();
            await fluidTemplate.RenderAsync(writer, System.Text.Encodings.Web.HtmlEncoder.Default, fluidContext);
            return Data.Ok(writer.ToString());
        }
        catch (Exception ex)
        {
            return Data.FromError(new ServiceError(
                $"Template render error: {ex.Message}", "RenderError", 500));
        }
    }

    /// <summary>
    /// Registers a type with Fluid's MemberAccessStrategy so its properties are accessible.
    /// Skips primitives, strings, and collections that Fluid already handles natively.
    /// </summary>
    private static void RegisterTypeIfNeeded(TemplateOptions options, object? value)
    {
        if (value == null) return;
        var type = value.GetType();
        if (type.IsPrimitive || type == typeof(string) || type == typeof(decimal)
            || type == typeof(DateTime) || type == typeof(DateTimeOffset)
            || type == typeof(Guid) || type.IsEnum)
            return;
        // Skip generic collections — Fluid handles IEnumerable natively
        if (type.IsArray) return;
        if (type.IsGenericType)
        {
            var genDef = type.GetGenericTypeDefinition();
            if (genDef == typeof(List<>) || genDef == typeof(Dictionary<,>)
                || genDef == typeof(HashSet<>))
                return;
        }
        try
        {
            options.MemberAccessStrategy.Register(type);
        }
        catch
        {
            // Some types may not be registerable — skip silently
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
    /// Resolves from the calling goal's directory, or engine root as fallback.
    /// </summary>
    private static string GetTemplateBaseDir(Render action)
    {
        var engine = action.Context.Engine;
        var goalPath = action.Context.Goal?.Path;
        if (!string.IsNullOrEmpty(goalPath))
        {
            var fs = engine.FileSystem;
            var goalDir = fs.Path.GetDirectoryName(goalPath);
            if (!string.IsNullOrEmpty(goalDir))
            {
                var pathData = new PathData(goalDir, action.Context);
                return pathData.Absolute;
            }
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
        var engine = (Engine.@this)context.AmbientValues["engine"];
        var plangContext = (PLangContext)context.AmbientValues["context"];

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
        catch (Exception ex)
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
                try
                {
                    // Use ValidatePath for proper resolution (handles PLang-rooted paths)
                    var fullPath = _fs.ValidatePath(_fs.Path.Combine(_basePath, candidate));
                    if (_fs.File.Exists(fullPath))
                        return new PlangFileInfo(_fs, fullPath, candidate);
                }
                catch
                {
                    // ValidatePath may throw for uninitialized filesystems —
                    // fall back to raw path combine
                    try
                    {
                        var rawPath = _fs.Path.Combine(_basePath, candidate);
                        if (_fs.File.Exists(rawPath))
                            return new PlangFileInfo(_fs, rawPath, candidate);
                    }
                    catch
                    {
                        continue;
                    }
                }
            }
            return new NotFoundFileInfo(subpath);
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
