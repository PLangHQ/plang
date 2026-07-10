using System.IO;
using Fluid;
using Fluid.Ast;
using Fluid.Values;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Primitives;
using app.type.path;
using app.error;
using app.type.path;
using app.goal;
using app.variable;

namespace app.module.ui.code;

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

    public async Task<data.@this<global::app.type.item.text.@this>> Render(Render action)
    {
        // Null-safe: [IsNotNull] guards the .pr path, but direct C# composition can
        // init Template to null — fail gracefully rather than throw.
        if (action.Template == null || (await action.Template.Value()) is not { } templateVal)
            return action.Context.Error<global::app.type.item.text.@this>(new global::app.error.ValidationError(
                "ui.render requires a template", "MissingTemplate"));
        var templateContent = templateVal.ToString() ?? "";
        var isFile = action.IsFile == null ? null : (await action.IsFile.Value());
        string? sourceFile = null;

        // Resolve template content: file or inline
        if (isFile?.Value == true || (isFile == null && LooksLikeFilePath(templateContent)))
        {
            var pathData = path.Resolve(templateContent, action.Context);
            if (!await pathData.AsBooleanAsync())
                return action.Context.Error<global::app.type.item.text.@this>(new ServiceError(
                    $"Template file not found: {templateContent}", "NotFound", 404));

            sourceFile = pathData.Relative;
            var readResult = await pathData.ReadText();
            if (!readResult.Success)
                return action.Context.Error<global::app.type.item.text.@this>(readResult.Error
                    ?? new ServiceError("Template read failed", "IOError", 500));
            templateContent = (await readResult.Value())?.ToString() ?? "";
        }

        // Parse
        var parser = CreateParser();
        if (!parser.TryParse(templateContent, out var fluidTemplate, out var parseError))
        {
            var location = sourceFile != null ? $" in '{sourceFile}'" : "";
            return action.Context.Error<global::app.type.item.text.@this>(new ServiceError(
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

        // Make PLang native collections / JsonNode Fluid-readable WITHOUT copying.
        // Fluid binds via reflection / IDictionary / IEnumerable only — it doesn't
        // use PLang's variable navigator — so a native dict/list (which implement
        // domain interfaces, not IDictionary/IEnumerable) and a JsonNode (what
        // `set … type=json` produces) render empty: `{{ x.key }}` / `{% for %}`
        // yield nothing even though `%x.key%` resolves in PLang. That silently
        // blanked the builder's compile prompt (picked actions + per-action schemas)
        // and made the compiler guess blind — the branch-wide build mis-maps.
        //
        // The converter wraps natives in lazy READ-THROUGH views (zero copy): a
        // dict reads keys on demand (O(1) member access), a list streams its
        // elements (Fluid arrays any IEnumerable eagerly anyway — same cost as a
        // real CLR list, no extra copy; we do NOT deep-copy via Clr). Nested
        // natives convert lazily because the converter re-runs at each member
        // access. A JsonNode routes through the universal parse to natives, then
        // the same views. Converters run wherever FluidValue.Create does — both
        // the variable-binding loops below and nested access during rendering.
        options.ValueConverters.Add(value => NativeCollectionConverter(value, action.Context));

        // The null citizen (typeless or typed-empty slot) renders as nothing and is
        // falsy — same as an undefined variable. Without this it would stringify via
        // ToString() to the literal "null".
        options.ValueConverters.Add(value =>
            value is global::app.type.item.@null.@this ? NilValue.Instance : null);

        // `formal` filter: renders any value the way the action catalog writes it —
        // strings with spaces/commas become quoted, %variables% stay bare, dicts/lists
        // become compact JSON, scalars use their literal form. Used by templates that
        // serialize action parameters into the formal "module.action Name([type] value)"
        // shape the builder LLM is trained on.
        options.Filters.AddFilter("formal", (input, args, context) =>
            new StringValue(FormatFormalValue(input.ToObjectValue())));

        // Configure file provider for {% include %} / {% render %} tags
        var basePath = GetTemplateBaseDir(action);
        options.FileProvider = new PlangFileProvider(action.Context.App, basePath, action.Context);

        var fluidContext = new TemplateContext(options);

        // Store app + Actor.Context.@this for callGoal tag access
        fluidContext.AmbientValues["app"] = action.Context.App;
        fluidContext.AmbientValues["context"] = action.Context;

        // Load Variables (GetAll already excludes !-prefixed). Use dictionary key as
        // the Fluid variable name — Data.Name is advisory and may differ.
        foreach (var kvp in action.Context.Variable.GetAll())
        {
            fluidContext.SetValue(kvp.Key, FluidValue.Create(await kvp.Value.Value(), options));
        }

        // Override with explicit parameters
        if ((action.Parameters == null ? null : await action.Parameters.Value()) != null)
        {
            foreach (var param in (await action.Parameters.Value())!.Items)
            {
                fluidContext.SetValue(param.Name, FluidValue.Create(await param.Value(), options));
            }
        }

        // Render with HTML encoding for security (XSS prevention)
        try
        {
            var writer = new StringWriter();
            await fluidTemplate.RenderAsync(writer, NullEncoder.Default, fluidContext);
            return action.Context.Ok<global::app.type.item.text.@this>(writer.ToString());
        }
        catch (Exception ex) when (ex is not (NullReferenceException or OutOfMemoryException or StackOverflowException))
        {
            var location = sourceFile != null ? $" in '{sourceFile}'" : "";
            return action.Context.Error<global::app.type.item.text.@this>(new ServiceError(
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
    /// Fluid value converter — turns a PLang native <c>dict</c>/<c>list</c> or a
    /// <c>JsonNode</c> into a lazy read-through view Fluid can navigate, without
    /// copying. Returns <c>null</c> for anything else so Fluid's own mapping runs.
    /// (See the registration site for why this is needed.)
    /// </summary>
    private static object? NativeCollectionConverter(object value, global::app.actor.context.@this context) => value switch
    {
        app.type.dict.@this d => new NativeDictView(d),
        app.type.list.@this l => new NativeListView(l),
        // JsonNode isn't Fluid-readable either; parse it to natives (the parse is
        // structural, JSON-DOM sized) and the natives then ride the views above.
        System.Text.Json.Nodes.JsonNode jn => new app.type.item.serializer.json(context).Parse(jn) switch
        {
            app.type.dict.@this d => new NativeDictView(d),
            app.type.list.@this l => new NativeListView(l),
            var scalar => scalar, // a bare JSON scalar — Fluid maps it directly
        },
        _ => null,
    };

    /// <summary>
    /// Lazy read-through <see cref="IDictionary{TKey,TValue}"/> over a native
    /// <c>dict</c> — Fluid maps <c>IDictionary&lt;string,object&gt;</c> to a
    /// dictionary value with O(1) keyed access, so a member read touches one entry
    /// (never copies the dict). Read-only: writes throw. Entry values stay raw so
    /// nested natives re-convert lazily on access.
    /// </summary>
    private sealed class NativeDictView(app.type.dict.@this d) : IDictionary<string, object?>
    {
        public object? this[string key]
        {
            get => d.Get(key)?.Peek();
            set => throw new NotSupportedException("template view is read-only");
        }
        public ICollection<string> Keys => d.KeyNames.ToList();
        public ICollection<object?> Values => d.Entries.Select(e => (object?)e.Peek()).ToList();
        public int Count => d.CountRaw;
        public bool IsReadOnly => true;
        public bool ContainsKey(string key) => d.Has(key);
        public bool TryGetValue(string key, out object? value)
        {
            var entry = d.Get(key);
            value = entry?.Peek();
            return entry != null;
        }
        public IEnumerator<KeyValuePair<string, object?>> GetEnumerator()
        {
            foreach (var e in d.Entries)
                yield return new KeyValuePair<string, object?>(e.Name, e.Peek());
        }
        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
        public void Add(string key, object? value) => throw new NotSupportedException("template view is read-only");
        public void Add(KeyValuePair<string, object?> item) => throw new NotSupportedException("template view is read-only");
        public bool Remove(string key) => throw new NotSupportedException("template view is read-only");
        public bool Remove(KeyValuePair<string, object?> item) => throw new NotSupportedException("template view is read-only");
        public void Clear() => throw new NotSupportedException("template view is read-only");
        public bool Contains(KeyValuePair<string, object?> item) => d.Has(item.Key);
        public void CopyTo(KeyValuePair<string, object?>[] array, int arrayIndex)
        {
            foreach (var kv in this) array[arrayIndex++] = kv;
        }
    }

    /// <summary>
    /// Lazy read-through <see cref="IList{T}"/> over a native <c>list</c>. Fluid
    /// arrays any <see cref="System.Collections.IEnumerable"/> eagerly (the same
    /// cost it pays for a real CLR list), so this streams the element values
    /// once with no extra copy. Read-only: writes throw. Element values stay raw
    /// so nested natives re-convert lazily.
    /// </summary>
    private sealed class NativeListView(app.type.list.@this l) : IList<object?>
    {
        public object? this[int index]
        {
            get => l.At(index)?.Peek();
            set => throw new NotSupportedException("template view is read-only");
        }
        public int Count => l.CountRaw;
        public bool IsReadOnly => true;
        public IEnumerator<object?> GetEnumerator()
        {
            foreach (var item in l.Items)
                yield return item.Peek();
        }
        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
        public int IndexOf(object? item) { for (int i = 0; i < l.CountRaw; i++) if (Equals(l.At(i)?.Peek(), item)) return i; return -1; }
        public bool Contains(object? item) => IndexOf(item) >= 0;
        public void CopyTo(object?[] array, int arrayIndex) { foreach (var v in this) array[arrayIndex++] = v; }
        public void Add(object? item) => throw new NotSupportedException("template view is read-only");
        public void Insert(int index, object? item) => throw new NotSupportedException("template view is read-only");
        public void RemoveAt(int index) => throw new NotSupportedException("template view is read-only");
        public bool Remove(object? item) => throw new NotSupportedException("template view is read-only");
        public void Clear() => throw new NotSupportedException("template view is read-only");
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
    private static global::app.type.path.@this GetTemplateBaseDir(Render action)
    {
        var context = action.Context;
        var goalPath = context.Goal?.Path;
        if (goalPath != null)
        {
            var goalDir = goalPath.Parent;
            if (goalDir != null) return goalDir;
        }
        return global::app.type.path.@this.Resolve("/", context);
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
                var output = (await result.Value())?.ToString() ?? "";
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
        private readonly global::app.@this _app;
        private readonly global::app.type.path.@this _basePath;
        private readonly global::app.actor.context.@this _context;

        public PlangFileProvider(global::app.@this app, global::app.type.path.@this basePath, global::app.actor.context.@this context)
        {
            _app = app;
            _basePath = basePath;
            _context = context;
        }

        public IFileInfo GetFileInfo(string subpath)
        {
            // Fluid appends ".liquid" to include paths — try both with and without
            var candidates = new[] { subpath, StripLiquidExtension(subpath) };
            foreach (var candidate in candidates)
            {
                if (string.IsNullOrEmpty(candidate)) continue;
                var resolved = TryResolvePath(candidate);
                if (resolved == null) continue;
                // ExistsAsync routes through AuthGate(Read). Out-of-root template
                // includes (`{% include '../../etc/passwd' %}`) surface as
                // permission prompts or denials — not silent file reads.
                var exists = resolved.ExistsAsync().GetAwaiter().GetResult();
                if (exists.Success && (exists.Peek() as global::app.type.item.@bool.@this)?.Value == true)
                    return new PlangFileInfo(resolved, candidate);
            }
            return new NotFoundFileInfo(subpath);
        }

        private global::app.type.path.@this? TryResolvePath(string candidate)
        {
            try
            {
                return _basePath.Combine(candidate);
            }
            catch (Exception ex) when (ex is not (NullReferenceException or OutOfMemoryException or StackOverflowException))
            {
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
        private readonly global::app.type.path.@this _path;

        public PlangFileInfo(global::app.type.path.@this path, string name)
        {
            _path = path;
            Name = name;
        }

        public bool Exists => true;
        public long Length
        {
            get
            {
                var stat = _path.Stat().GetAwaiter().GetResult();
                return stat.Success && (stat.Peek() as global::app.type.path.@this.StatInfo)?.Length is long n ? n : 0;
            }
        }
        public string? PhysicalPath => _path.Absolute;
        public string Name { get; }
        public DateTimeOffset LastModified => DateTimeOffset.UtcNow;
        public bool IsDirectory => false;

        public Stream CreateReadStream()
        {
            // ReadText routes through AuthGate(Read) — out-of-root templates
            // surface as denials before any disk access. Template MIMEs that
            // map to byte[] (octet-stream, unmapped extensions) come back as
            // raw bytes; UTF-8 decode them. String-valued reads pass through.
            var read = _path.ReadText().GetAwaiter().GetResult();
            string content;
            if (read.Peek() is global::app.type.binary.@this bin)
                content = System.Text.Encoding.UTF8.GetString(bin.Value);
            else
                content = read.Peek()?.ToString() ?? "";
            return new MemoryStream(System.Text.Encoding.UTF8.GetBytes(content));
        }
    }
}
