using System.IO;
using Fluid;
using Fluid.Ast;
using Fluid.Values;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Primitives;
using app.type.item.path;
using app.error;
using app.type.item.path;
using app.goal;

namespace app.module.action.ui.code;

public class Fluid : ITemplate
{
    public string Name => "default";
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
            if (!await pathData.AsBooleanAsync(action.Context))
                return action.Context.Error<global::app.type.item.text.@this>(new ServiceError(
                    $"Template file not found: {templateContent}", "NotFound", 404));

            sourceFile = pathData.Relative(action.Context);
            var readResult = await pathData.Read(action.Context);
            var read = readResult.Success ? await readResult.Value() : null;
            if (!readResult.Success)
                return action.Context.Error<global::app.type.item.text.@this>(readResult.Error
                    ?? new ServiceError("Template read failed", "IOError", 500));
            templateContent = read?.ToString() ?? "";
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
        // A plang value navigates by its OWN door (Data.Get — the same navigation list.where /
        // condition use), not by C# reflection. Reflection reads a clr HOST carrier's surface
        // (Kind/Value/Context), never the host's own members — so `{{ module.Name }}` over a
        // carried host reads blank. The strategy routes member access on any plang item through
        // Get so a host, a scalar's backing, and a navigated child all resolve through one door;
        // everything else falls back to reflection. A container is converted before member access
        // (Item, below), so this catches a leaf handed to Fluid whole (a date's .Ticks).
        options.MemberAccessStrategy = new PlangDoorStrategy(action.Context) { IgnoreCasing = true };

        // The null citizen (typeless or typed-empty slot) renders as nothing and is
        // falsy — same as an undefined variable. Without this it would stringify via
        // ToString() to the literal "null".
        options.ValueConverters.Add(value =>
            value is global::app.type.item.@null.@this ? NilValue.Instance : null);

        // A plang container — a dict, a list, a json host, a file's content, any value that is not a
        // leaf — is one Fluid value that reads through the item's own doors (Item, below), whatever the
        // container is. Converters run wherever FluidValue.Create does — the variable-binding loops
        // below and every member or element a render reaches.
        options.ValueConverters.Add(value =>
            value is global::app.type.item.@this { IsLeaf: false } container ? Item.Liquid(container, action.Context, options) : null);


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
        if ((action.Parameter == null ? null : await action.Parameter.Value()) != null)
        {
            foreach (var param in (await action.Parameter.Value())!.Items(action.Context))
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
            // Fluid wraps a member/converter fault as TargetInvocationException whose Message is the
            // useless "Exception has been thrown by the target of an invocation" — dig to the real
            // inner fault (and its type) so a bad navigation/coercion in the template is named.
            var root = ex;
            while (root.InnerException != null) root = root.InnerException;
            var detail = ReferenceEquals(root, ex) ? ex.Message : $"{root.GetType().Name}: {root.Message}";
            return action.Context.Error<global::app.type.item.text.@this>(new ServiceError(
                $"Template render error{location}: {detail}", "RenderError", 500) { Exception = ex });
        }
    }


    /// <summary>
    /// Member access for plang values goes through <c>Data.Get</c> — the value's OWN
    /// navigation door — instead of C# reflection. A plang item routes to the door accessor;
    /// any other type falls back to reflection (the base <see cref="UnsafeMemberAccessStrategy"/>).
    /// </summary>
    internal sealed class PlangDoorStrategy : MemberAccessStrategy
    {
        // UnsafeMemberAccessStrategy is sealed, so reflection fallback is COMPOSED, not inherited:
        // a plang item routes to the door; every other type (Goal, Step, POCO) reflects as before.
        private readonly PlangDoorAccessor _door;
        private readonly UnsafeMemberAccessStrategy _reflection = new() { IgnoreCasing = true, MemberNameStrategy = MemberNameStrategies.Default };

        public PlangDoorStrategy(global::app.actor.context.@this context)
        {
            _door = new PlangDoorAccessor(context);
            IgnoreCasing = true;
        }

        public override IMemberAccessor GetAccessor(Type type, string name)
            => typeof(global::app.type.item.@this).IsAssignableFrom(type)
                ? _door
                : _reflection.GetAccessor(type, name);

        // Registration (the {% for %}/member map) is the reflection strategy's job — plang items
        // never register, they navigate through the door.
        public override void Register(Type type, IEnumerable<KeyValuePair<string, IMemberAccessor>> accessors)
            => _reflection.Register(type, accessors);
    }

    /// <summary>Navigates a plang item by member name through its <c>Data.Get</c> door — the same
    /// navigation <c>list.where</c>/<c>condition</c> use, so templates and predicates read a value
    /// the one way. Async because a door can be I/O (a path's existence, a computed value).</summary>
    private sealed class PlangDoorAccessor(global::app.actor.context.@this context) : IAsyncMemberAccessor
    {
        public async Task<object> GetAsync(object obj, string name, TemplateContext ctx)
        {
            // Resolve through the Value door (references/computed/prose resolve), then lower a LEAF
            // to its raw backing so Fluid sees a real bool/string/number (truthiness, comparison,
            // `where:`); a container or host passes through as its item — the converters wrap a
            // dict/list into a view, a host re-enters this door on its next member. A value wanted in
            // its AUTHORED form is never member-accessed here — it's embedded via the `store` filter,
            // which drives the value's own Store writer (%refs% literal) instead of this resolve door.
            var resolved = await (await new global::app.data.@this("", obj, context: context).Get(name)).Value();
            return (resolved is global::app.type.item.@this value ? value.Backing : resolved)!;
        }

        // The value door is async (a path stat, a computed render). Templates render via
        // RenderAsync, so Fluid always takes GetAsync — the sync face would have to block on
        // the async door (sync-over-async), so it refuses instead of deadlocking.
        public object Get(object obj, string name, TemplateContext ctx)
            => throw new System.NotSupportedException(
                "plang template member access is async — render through RenderAsync, not the sync path.");
    }

    /// <summary>
    /// A plang container as a template sees it — any item that is not a leaf, read through the item's own
    /// doors: a member or an index through its <c>Get</c> (a file reference narrows to its content there, a
    /// json host descends by its kind), its children through its <c>EnumerateItems</c>. It reaches Fluid in
    /// liquid's own two shapes, as the item says it holds its children (<see cref="global::app.type.item.@this.IsSequence"/>):
    /// by position, an array — so <c>join</c>, <c>sort</c>, <c>map</c>, <c>where</c> act on it; by name, a hash —
    /// a <c>{% for %}</c> yields <c>[key, value]</c> pairs (<c>{% for c in decider.common %}{{ c[0] }}</c>). A leaf
    /// reached is handed to Fluid as its raw backing, so truthiness, comparison and <c>where:</c> see a real
    /// string or number.
    /// </summary>
    private sealed class Item(global::app.type.item.@this item, global::app.actor.context.@this context) : ObjectValueBase(item)
    {
        /// <summary><paramref name="container"/> in the liquid shape it holds its children in.</summary>
        internal static FluidValue Liquid(global::app.type.item.@this container, global::app.actor.context.@this context,
            TemplateOptions options)
            => container.IsSequence
                ? new ArrayValue(container.EnumerateItems(context).Select(pair => Lowered(pair.value.Peek(), options)).ToList())
                : new Item(container, context);

        public override async ValueTask<FluidValue> GetValueAsync(string name, TemplateContext ctx)
            => Lowered(await (await item.Get(Parent, name)).Value(), ctx.Options);

        public override async ValueTask<FluidValue> GetIndexAsync(FluidValue index, TemplateContext ctx)
            => Lowered(await (await item.Get(Parent, index.ToStringValue(), isIndex: index.Type == FluidValues.Number)).Value(), ctx.Options);

        // A hash iterates as [key, value] pairs.
        public override IEnumerable<FluidValue> Enumerate(TemplateContext ctx)
            => item.EnumerateItems(context).Select(pair =>
                (FluidValue)new ArrayValue(new[] { Lowered(pair.key.Peek(), ctx.Options), Lowered(pair.value.Peek(), ctx.Options) }));

        public override string ToStringValue() => item.ToString() ?? "";

        // The item navigated from — its own Data, with the render's context.
        private global::app.data.@this Parent => new("", item, context: context);

        private static FluidValue Lowered(object? value, TemplateOptions options)
            => FluidValue.Create(value is global::app.type.item.@this reached ? reached.Backing : value, options);
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
    private static global::app.type.item.path.@this GetTemplateBaseDir(Render action)
    {
        var context = action.Context;
        var goalPath = context.CallStack.Goal?.Path;
        if (goalPath != null)
        {
            var goalDir = goalPath.Parent;
            if (goalDir != null) return goalDir;
        }
        return global::app.type.item.path.@this.Resolve("/", context);
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

        // A failed call fails the render — the render's result is the error, never an
        // "[Error: …]" printed into output that reads as success.
        var goalNameValue = await expression.EvaluateAsync(context);
        var goalName = goalNameValue?.ToStringValue() ?? "";
        if (string.IsNullOrEmpty(goalName))
            throw new global::app.error.AppException("callGoal requires a goal name", "MissingGoalName", 400);

        var goal = await app.goal.list.Find(goalName);
        if (goal == null)
            throw new global::app.error.GoalNotFoundException(goalName);

        var result = await goal.Start(plangContext);
        if (!result.Success)
            throw new global::app.error.AppException($"callGoal '{goalName}' failed: {result.Error?.Message}",
                result.Error?.Key ?? "GoalFailed", result.Error?.StatusCode ?? 500);

        await writer.WriteAsync((await result.Value())?.ToString() ?? "");
        return Completion.Normal;
    }

    // --- Microsoft.Extensions.FileProviders adapter for Fluid's include/render tags ---

    private sealed class PlangFileProvider : IFileProvider
    {
        private readonly global::app.@this _app;
        private readonly global::app.type.item.path.@this _basePath;
        private readonly global::app.actor.context.@this _context;

        public PlangFileProvider(global::app.@this app, global::app.type.item.path.@this basePath, global::app.actor.context.@this context)
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
                var resolved = _basePath.Combine(candidate);
                // ExistsAsync routes through AuthGate(Read). Out-of-root template
                // includes (`{% include '../../etc/passwd' %}`) surface as
                // permission prompts or denials — not silent file reads. A failed
                // probe (denied, unreadable) fails the render; only "absent" is not-found.
                var exists = resolved.ExistsAsync(_context).GetAwaiter().GetResult();
                if (!exists.Success)
                    throw new global::app.error.AppException($"include '{candidate}': {exists.Error?.Message}",
                        exists.Error?.Key ?? "IncludeFailed", exists.Error?.StatusCode ?? 500);
                if ((exists.Peek() as global::app.type.item.@bool.@this)?.Value == true)
                    return new PlangFileInfo(resolved, candidate, _context);
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
        private readonly global::app.type.item.path.@this _path;
        private readonly global::app.actor.context.@this _context;

        public PlangFileInfo(global::app.type.item.path.@this path, string name, global::app.actor.context.@this context)
        {
            _path = path;
            Name = name;
            _context = context;
        }

        public bool Exists => true;
        public long Length
        {
            get
            {
                var stat = _path.Stat(_context).GetAwaiter().GetResult();
                return stat.Success && (stat.Peek() as global::app.type.item.path.@this.StatInfo)?.Length is long n ? n : 0;
            }
        }
        public string? PhysicalPath => _path.Absolute;
        public string Name { get; }
        public DateTimeOffset LastModified => DateTimeOffset.UtcNow;
        public bool IsDirectory => false;

        public Stream CreateReadStream()
        {
            // The include lands as a reference through AuthGate(Read) — out-of-root templates
            // surface as denials before any disk access. Template MIMEs that
            // map to bytes (octet-stream, unmapped extensions) come back as
            // raw bytes; UTF-8 decode them. Text passes through.
            var landed = _path.Read(_context).GetAwaiter().GetResult();
            var read = landed.Success ? landed.Value().AsTask().GetAwaiter().GetResult() : null;
            string content;
            if (read is global::app.type.item.binary.@this bin)
                content = System.Text.Encoding.UTF8.GetString(bin.Value);
            else
                content = read?.ToString() ?? "";
            return new MemoryStream(System.Text.Encoding.UTF8.GetBytes(content));
        }
    }
}
