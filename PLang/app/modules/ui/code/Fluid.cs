using System.IO;
using Fluid;
using Fluid.Ast;
using Fluid.Values;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Primitives;
using app.filesystem;
using app.filesystem.Default;
using app.errors;
using app.filesystem;
using app.goals.goal;
using app.variables;

namespace app.modules.ui.code;

public class Fluid : ITemplate
{
    public string Name => "default";
    public bool IsDefault { get; set; }
    public bool IsBuiltIn { get; set; }
    public string? Source { get; set; }

    private FluidParser CreateParser()
    {
        var parser = new FluidParser();
        parser.RegisterExpressionTag("callGoal", CallGoalTagAsync);
        return parser;
    }

    public async Task<data.@this> Render(Render action)
    {
        var templateContent = action.Template.Value!;
        var isFile = action.IsFile?.Value;
        string? sourceFile = null;

        // Resolve template content: file or inline
        if (isFile == true || (isFile == null && LooksLikeFilePath(templateContent)))
        {
            var pathData = global::app.filesystem.path.Resolve(templateContent, action.Context);
            if (!pathData.Exists)
                return app.data.@this.FromError(new ServiceError(
                    $"Template file not found: {templateContent}", "NotFound", 404));

            sourceFile = pathData.Relative;
            try
            {
                var fs = action.Context.App.FileSystem;
                templateContent = fs.File.ReadAllText(pathData.Absolute);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                return app.data.@this.FromError(new ServiceError(ex.Message, "IOError", 500));
            }
        }

        // Parse
        var parser = CreateParser();
        if (!parser.TryParse(templateContent, out var fluidTemplate, out var parseError))
        {
            var location = sourceFile != null ? $" in '{sourceFile}'" : "";
            return app.data.@this.FromError(new ServiceError(
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

        // `formal` filter: renders any value the way the action catalog writes it —
        // strings with spaces/commas become quoted, %variables% stay bare, dicts/lists
        // become compact JSON, scalars use their literal form. Used by templates that
        // serialize action parameters into the formal "module.action Name([type] value)"
        // shape the builder LLM is trained on.
        options.Filters.AddFilter("formal", (input, args, ctx) =>
            new StringValue(FormatFormalValue(input.ToObjectValue())));

        // Configure file provider for {% include %} / {% render %} tags
        var fs2 = action.Context.App.FileSystem;
        var basePath = GetTemplateBaseDir(action);
        options.FileProvider = new PlangFileProvider(fs2, basePath);

        var fluidContext = new TemplateContext(options);

        // Store app + Actor.Context.@this for callGoal tag access
        fluidContext.AmbientValues["app"] = action.Context.App;
        fluidContext.AmbientValues["context"] = action.Context;

        // Load Variables (GetAll already excludes !-prefixed). Use dictionary key as
        // the Fluid variable name — Data.Name is advisory and may differ.
        foreach (var kvp in action.Context.Variables.GetAll())
        {
            fluidContext.SetValue(kvp.Key, FluidValue.Create(kvp.Value.Value, options));
        }

        // Override with explicit parameters
        if (action.Parameters?.Value != null)
        {
            foreach (var param in action.Parameters.Value)
            {
                fluidContext.SetValue(param.Name, FluidValue.Create(param.Value, options));
            }
        }

        // Render with HTML encoding for security (XSS prevention)
        try
        {
            var writer = new StringWriter();
            await fluidTemplate.RenderAsync(writer, NullEncoder.Default, fluidContext);
            return app.data.@this.Ok(writer.ToString());
        }
        catch (Exception ex) when (ex is not (NullReferenceException or OutOfMemoryException or StackOverflowException))
        {
            var location = sourceFile != null ? $" in '{sourceFile}'" : "";
            return app.data.@this.FromError(new ServiceError(
                $"Template render error{location}: {ex.Message}", "RenderError", 500));
        }
    }

    /// <summary>
    /// Render a value in the catalog's formal syntax. Mirrors the rules used by
    /// Default.FormatValue for the trace-backfill path, so the
    /// builder's rebuild prompt and the viewer's formal string stay consistent.
    /// </summary>
    private static string FormatFormalValue(object? v)
    {
        v = UnwrapFluid(v);
        if (v == null) return "null";
        if (v is string s)
        {
            if (s.StartsWith('%')) return s;
            if (s.Contains(' ') || s.Contains(',')) return $"\"{s}\"";
            return s;
        }
        if (v is bool b) return b ? "true" : "false";
        // Scalars (numbers, enums, any primitive-like IConvertible) use their literal form.
        // InvariantCulture so the formatted text round-trips with TypeConverter's
        // InvariantCulture parse — without this, it-IT/de-DE writes "3,14" and the
        // parse FormatExceptions on the comma.
        if (v is IConvertible conv) return System.Convert.ToString(conv, System.Globalization.CultureInfo.InvariantCulture) ?? "";
        // Everything else — dicts, lists, POCOs — render as JSON.
        try { return System.Text.Json.JsonSerializer.Serialize(v); }
        catch (System.Exception ex) when (ex is System.Text.Json.JsonException || ex is NotSupportedException) { return v.ToString() ?? ""; }
    }

    /// <summary>
    /// Fluid's <c>FluidValue.Create</c> wraps .NET dictionaries in
    /// <c>ObjectDictionaryFluidIndexable&lt;T&gt;</c>. <c>ToObjectValue()</c>
    /// returns the wrapper, not the underlying dict — so JSON-serializing it
    /// emits the wrapper's surface (Count, Keys), not the contents. Unwrap
    /// recursively so the filter can render structured values as real JSON.
    /// </summary>
    private static object? UnwrapFluid(object? v)
    {
        if (v is IFluidIndexable indexable)
        {
            var dict = new Dictionary<string, object?>();
            foreach (var key in indexable.Keys)
            {
                indexable.TryGetValue(key, out var inner);
                dict[key] = UnwrapFluid(inner?.ToObjectValue());
            }
            return dict;
        }
        if (v is System.Collections.IEnumerable e && v is not string and not System.Collections.IDictionary)
        {
            var list = new List<object?>();
            foreach (var item in e) list.Add(UnwrapFluid(item));
            return list;
        }
        return v;
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
        return ext.Length >= 2 && ext.Length <= 10 && !ext.Contains(' ');
    }

    /// <summary>
    /// Gets the base directory for template file resolution (includes).
    /// Resolves from the calling goal's directory, or app root as fallback.
    /// </summary>
    private static string GetTemplateBaseDir(Render action)
    {
        var app = action.Context.App;
        var goalPath = action.Context.Goal?.Path;
        if (!string.IsNullOrEmpty(goalPath))
        {
            var fs = app.FileSystem;
            var goalDir = fs.Path.GetDirectoryName(goalPath);
            if (!string.IsNullOrEmpty(goalDir))
                return fs.ValidatePath(goalDir);
        }
        return app.AbsolutePath;
    }

    /// <summary>
    /// Custom Fluid tag handler for {% callGoal 'GoalName' %} or {% callGoal GoalName %}.
    /// Invokes a PLang goal and writes the result into template output.
    /// </summary>
    private static async ValueTask<Completion> CallGoalTagAsync(
        global::Fluid.Ast.Expression expression, TextWriter writer, System.Text.Encodings.Web.TextEncoder encoder,
        TemplateContext context)
    {
        var app = (app.@this)context.AmbientValues["app"];
        var plangContext = (global::app.actor.context.@this)context.AmbientValues["context"];

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
            var result = await app.RunGoalAsync(goalCall, plangContext);

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
