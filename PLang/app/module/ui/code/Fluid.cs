using System.IO;
using Fluid;
using Fluid.Ast;
using Fluid.Values;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Primitives;
using app.type.item.path;
using app.error;
using app.goal;

namespace app.module.ui.code;

public class Fluid : ITemplate
{
    public string Name => "default";
    public bool IsBuiltIn { get; set; }
    public string? Source { get; set; }

    // The parser with plang's tags and operators.
    private FluidParser Parser()
    {
        var parser = new FluidParser();
        parser.RegisterExpressionTag("callGoal", CallGoal);
        // A template's order (`>`, `<`, `>=`, `<=`) is the values' own: liquid's operators order only its own number
        // and string classes, so a plang value would never order.
        parser.RegisteredOperators[">"] = (left, right) => new Ordered(left, right, global::app.data.Comparison.Greater, false);
        parser.RegisteredOperators[">="] = (left, right) => new Ordered(left, right, global::app.data.Comparison.Greater, true);
        parser.RegisteredOperators["<"] = (left, right) => new Ordered(left, right, global::app.data.Comparison.Less, false);
        parser.RegisteredOperators["<="] = (left, right) => new Ordered(left, right, global::app.data.Comparison.Less, true);
        return parser;
    }

    /// <summary>
    /// A template's order between two values, asked of the values themselves: each a plang value (a liquid literal
    /// made the value the render's context makes of it), ordered by its own <c>Compare</c>. True when the order is
    /// <paramref name="wanted"/>, or equal when <paramref name="orEqual"/>; a value with no order to the other
    /// (nil, a dict against a number) is false.
    /// </summary>
    private sealed class Ordered(global::Fluid.Ast.Expression left, global::Fluid.Ast.Expression right,
        global::app.data.Comparison wanted, bool orEqual) : global::Fluid.Ast.BinaryExpression(left, right)
    {
        public override async ValueTask<FluidValue> EvaluateAsync(TemplateContext ctx)
        {
            var context = (global::app.actor.context.@this)ctx.AmbientValues["context"];
            var a = await Left.EvaluateAsync(ctx);
            var b = await Right.EvaluateAsync(ctx);
            if (a.IsNil() || b.IsNil()) return BooleanValue.False;
            var order = await Plang(a, context).Compare(Plang(b, context), context);
            return BooleanValue.Create(order == wanted || (orEqual && order == global::app.data.Comparison.Equal));
        }

        // the plang value a liquid value stands for: one it holds whole, else the value its raw form makes
        private static global::app.type.item.@this Plang(FluidValue value, global::app.actor.context.@this context)
            => value is Item plang ? plang.Held : global::app.type.item.@this.Create(value.ToObjectValue(), context);
    }

    public async Task<global::app.data.@this> Render(Render action)
    {
        // Null-safe: [IsNotNull] guards the .pr path, but direct C# composition can
        // init Template to null — fail gracefully rather than throw.
        if (action.Template == null || (await action.Template.Value()) is not { } template)
            return action.Context.Error(new global::app.error.ValidationError(
                "ui.render requires a template", "MissingTemplate"));
        // the template's characters, handed to Fluid's parser (the .NET boundary)
        var templateContent = template.Clr<string>() ?? "";
        var isFile = action.IsFile == null ? null : (await action.IsFile.Value());
        string? sourceFile = null;
        // What the render is: the content-kind entity the template file's extension names (a .json template is
        // {item, json}) — the format whose own birth the rendered characters go through. An inline template, or a
        // file of plain text, has no format and is born a plain text.
        global::app.type.@this? format = null;

        // Resolve template content: file or inline
        if (isFile?.Value == true || (isFile == null && IsPath(templateContent)))
        {
            var path = global::app.type.item.path.@this.Resolve(templateContent, action.Context);
            if (!await path.AsBooleanAsync(action.Context))
                return action.Context.Error(new ServiceError(
                    $"Template file not found: {templateContent}", "NotFound", 404));

            sourceFile = path.Relative(action.Context).Raw;
            format = path.Kind(action.Context) is { kind.IsEmpty: false } formatted ? formatted : null;
            // a template file is UTF-8 text, whatever its extension
            var bytes = await path.Bytes(action.Context);
            if (!bytes.Success) return action.Context.Error(bytes.Error!);
            templateContent = System.Text.Encoding.UTF8.GetString((await bytes.Value())?.Value ?? []);
        }

        // Parse
        var parser = Parser();
        if (!parser.TryParse(templateContent, out var fluidTemplate, out var parseError))
        {
            var location = sourceFile != null ? $" in '{sourceFile}'" : "";
            return action.Context.Error(new ServiceError(
                $"Template syntax error{location}: {parseError}", "TemplateError", 400));
        }

        // Build context
        var options = new TemplateOptions();
        options.MaxSteps = 100_000; // Defense-in-depth: prevent pathological templates from running indefinitely
        options.MaxRecursion = 100; // Prevent deeply recursive includes
        // A plang value navigates by its OWN door (Data.Get — the same navigation list.query /
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

        // A plang value — a dict, a list, a file, a number, a duration, any value — is one Fluid value that answers
        // each question Liquid asks through the value's own doors (Item, below): how it shows (its Output), whether
        // it is true, how it compares, what it holds. Converters run wherever FluidValue.Create does — the
        // variable-binding loops below and every member or element a render reaches.
        var culture = (await action.Context.Setting.Get<global::app.setting.@this>()).Culture;
        options.ValueConverters.Add(value =>
            value is global::app.type.item.@this item ? Item.Liquid(item, action.Context, options, culture) : null);

        // `{{ x | json }}` writes the value through plang's json format, as the json writer shows it (liquid's
        // `json: true` indent is not honoured). Liquid's own json filter reflects the C# object behind the value
        // (IsSequence, RawBytes, …). The value is taken as it is: a text holding %…% is those characters. A page
        // renders it as it is (no encoder), so the json is made safe for the page here, whatever the json format
        // writes: < > & escaped, which json reads back the same — they can stand only inside a json string, so a
        // value's </script> never closes the page's script.
        options.Filters.AddFilter("json", async (input, _, _) =>
        {
            var value = new global::app.data.@this("", input is Item plang ? plang.Held
                : global::app.type.item.@this.Create(input.ToObjectValue(), action.Context), context: action.Context);
            using var json = new MemoryStream();
            var answer = await action.Context.App.type.list.Mime("application/json").Encode(json, value, action.Context);
            if (!answer.Success) throw new AppException(answer.Error!);
            return new StringValue(System.Text.Encoding.UTF8.GetString(json.ToArray())
                .Replace("<", "\\u003C").Replace(">", "\\u003E").Replace("&", "\\u0026"));
        });

        // `{{ x | text }}` writes the value as plang's text format writes it — a leaf bare, a container as its json:
        // the value's own text form, never the C# object's ToString.
        options.Filters.AddFilter("text", async (input, _, _) =>
        {
            var value = new global::app.data.@this("", input is Item plang ? plang.Held
                : global::app.type.item.@this.Create(input.ToObjectValue(), action.Context), context: action.Context);
            using var text = new MemoryStream();
            var answer = await action.Context.App.type.list.Mime("text/plain").Encode(text, value, action.Context);
            if (!answer.Success) throw new AppException(answer.Error!);
            return new StringValue(System.Text.Encoding.UTF8.GetString(text.ToArray()));
        });

        // Configure file provider for {% include %} / {% render %} tags
        var includes = new PlangFileProvider(Folder(action), action.Context);
        options.FileProvider = includes;

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
            // Fluid asks for an include through a synchronous file provider: every include the template
            // names is read first, through the gate, so the render reads them from memory.
            await includes.Load(fluidTemplate, parser);
            var writer = new StringWriter();
            await fluidTemplate.RenderAsync(writer, NullEncoder.Default, fluidContext);
            var rendered = writer.ToString();
            // The rendered characters go through the declared format's OWN birth (kind.Decode — the same door a
            // url served that format decodes through, content.@this): a .json template is born an unread
            // {item, json}. A plain or inline template has no format and is born a text. Never a late
            // stamp — json is the item's kind, never put onto a text.
            // The rendered characters ARE text. A format a program then navigates as a structure — json, the
            // item's kind — rests as that structure through its own birth, so a rendered .json is an
            // unread {item, json}. Any other format (html, md: a code/text format) is the rendered characters
            // themselves, a plain text — never a late stamp, and never a non-text value that a text comparison
            // then refuses.
            return format is { Name: "item" }
                ? await format.kind.Decode(System.Text.Encoding.UTF8.GetBytes(rendered), action.Context)
                : action.Context.Ok(new global::app.type.item.text.@this(rendered,
                    action.Context.App.type.list[typeof(global::app.type.item.text.@this)]));
        }
        // a program's error inside the template — an unknown goal, a failed include — is the render's answer, whole
        catch (AppException ex)
        {
            return action.Context.Error(ex.Error);
        }
        // Fluid reaches a member by reflection, which wraps what the member threw
        catch (System.Reflection.TargetInvocationException ex) when (ex.InnerException is AppException thrown)
        {
            return action.Context.Error(thrown.Error);
        }
        catch (Exception ex) when (ex is not (NullReferenceException or OutOfMemoryException or StackOverflowException))
        {
            var location = sourceFile != null ? $" in '{sourceFile}'" : "";
            // Its Message is the useless "Exception has been thrown by the target of an invocation" — dig to
            // the real inner fault (and its type) so a bad navigation/coercion in the template is named.
            var root = ex;
            while (root.InnerException != null) root = root.InnerException;
            var detail = ReferenceEquals(root, ex) ? ex.Message : $"{root.GetType().Name}: {root.Message}";
            return action.Context.Error(new ServiceError(
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
    /// navigation <c>list.query</c>/<c>condition</c> use, so templates and predicates read a value
    /// the one way. Async because a door can be I/O (a path's existence, a computed value).</summary>
    private sealed class PlangDoorAccessor(global::app.actor.context.@this context) : IAsyncMemberAccessor
    {
        public async Task<object> GetAsync(object obj, string name, TemplateContext ctx)
        {
            // Resolve through the Value door (references/computed/prose resolve); the value passes through as its
            // item, which the converter makes the one Fluid value that answers for it (its text, truth, order and
            // members its own). A value wanted in
            // its AUTHORED form is never member-accessed here — it's embedded via the `store` filter,
            // which drives the value's own Store writer (%refs% literal) instead of this resolve door.
            var member = await new global::app.data.@this("", obj, context: context).Get(name);
            var resolved = await member.Value();
            // a member that is not there is nil (`{% if a.Examples %}` over an absent file); one that is there and
            // could not be read (an I/O error, a timeout, a refused read) fails the render — never an empty value
            if (member.Error is { Status.IsNotFound: false } failed) throw new AppException(failed);
            // the plang value whole: it answers for itself in the template (Item, below)
            return resolved!;
        }

        // The value door is async (a path stat, a computed render). Templates render via
        // RenderAsync, so Fluid always takes GetAsync — the sync face would have to block on
        // the async door (sync-over-async), so it refuses instead of deadlocking.
        public object Get(object obj, string name, TemplateContext ctx)
            => throw new System.NotSupportedException(
                "plang template member access is async — render through RenderAsync, not the sync path.");
    }

    /// <summary>
    /// A plang value as a template sees it — any item, answering each question Liquid asks through its own doors:
    /// how it shows, its Output written as text in the render's culture (<c>30s</c>, <c>7,47</c> in is-IS), never a
    /// ToString; whether it is true, its own truthiness; how it equals and orders, its own <c>Compare</c>; a member or
    /// an index through its <c>Get</c> (a file reference narrows to its content there, a json host descends by its
    /// kind); its children through its <c>EnumerateItems</c>. A value that holds its children by position reaches
    /// Fluid as liquid's array (<see cref="global::app.type.item.@this.IsSequence"/>) — so <c>join</c>, <c>sort</c>,
    /// <c>map</c>, <c>where</c> act on it; any other iterates as <c>[key, value]</c> pairs
    /// (<c>{% for c in decider.common %}{{ c[0] }}</c>).
    /// </summary>
    private sealed class Item(global::app.type.item.@this item, global::app.actor.context.@this context, TemplateOptions options,
            global::app.type.item.culture.@this culture)
        : ObjectValueBase(item)
    {
        /// <summary><paramref name="value"/> in the liquid shape it holds its children in, its numbers shown in
        /// <paramref name="culture"/> (the render's, read once before it starts: liquid asks for text synchronously).</summary>
        internal static FluidValue Liquid(global::app.type.item.@this value, global::app.actor.context.@this context,
            TemplateOptions options, global::app.type.item.culture.@this culture)
            => value.IsSequence
                ? new ArrayValue(value.EnumerateItems(context).Select(pair => FluidValue.Create(pair.value.Peek(), options)).ToList())
                : new Item(value, context, options, culture);

        /// <summary>What liquid takes the value for when it compares (<c>where:</c>, <c>==</c>, <c>size</c>): a leaf
        /// what its raw form is to liquid (a text a string, a number a number), anything else an object.</summary>
        public override FluidValues Type
            => item.IsLeaf ? FluidValue.Create(item.Backing, options).Type : FluidValues.Object;

        public override async ValueTask<FluidValue> GetValueAsync(string name, TemplateContext ctx)
            => FluidValue.Create(await Read(await item.Get(Parent, name)), ctx.Options);

        public override async ValueTask<FluidValue> GetIndexAsync(FluidValue index, TemplateContext ctx)
            => FluidValue.Create(await Read(await item.Get(Parent, index.ToStringValue(), isIndex: index.Type == FluidValues.Number)), ctx.Options);

        // a child that is not there is nil; one that is there and could not be read fails the render
        private async ValueTask<global::app.type.item.@this?> Read(global::app.data.@this child)
        {
            var value = await child.Value();
            if (child.Error is { Status.IsNotFound: false } failed) throw new AppException(failed);
            return value;
        }

        // A hash iterates as [key, value] pairs.
        public override IEnumerable<FluidValue> Enumerate(TemplateContext ctx)
            => item.EnumerateItems(context).Select(pair =>
                (FluidValue)new ArrayValue(new[] { FluidValue.Create(pair.key.Peek(), ctx.Options), FluidValue.Create(pair.value.Peek(), ctx.Options) }));

        /// <summary>How the value shows: its Output, written as text in the render's culture. A value reaches a
        /// template already read (its door opened), so its Output completes at once; a write that failed answers its
        /// own error, and one still waiting on I/O is refused, never blocked on.</summary>
        public override string ToStringValue()
        {
            var stream = new MemoryStream();
            var text = Writer(stream);
            // liquid asks for the text synchronously (ToStringValue has no async form), so the write is never awaited
            // here: one that hasn't completed at once is refused, and keeps the stream it is still writing into
            var output = item.Output(text);
            if (!output.IsCompleted) throw new AppException(new global::app.error.@this(
                "a template shows a value whose output waits on I/O — read it first", "TemplateValueUnread", 400));
            using (stream)
            {
                // completed: a write that failed re-raises its own error
                output.GetAwaiter().GetResult();
                return System.Text.Encoding.UTF8.GetString(stream.ToArray());
            }
        }

        public override async ValueTask WriteToAsync(TextWriter writer, System.Text.Encodings.Web.TextEncoder encoder,
            System.Globalization.CultureInfo cultureInfo)
        {
            using var stream = new MemoryStream();
            var text = Writer(stream);
            await item.Output(text);
            writer.Write(encoder.Encode(System.Text.Encoding.UTF8.GetString(stream.ToArray())));
        }

        // plang's text writer over the stream, its numbers in the render's culture
        private global::app.type.item.text.Writer Writer(Stream into)
            => new(into, System.Text.Encoding.UTF8, culture, global::app.View.Out, context);

        /// <summary>Whether the value is true — its own truthiness (0, "", false and null are not).</summary>
        public override bool ToBooleanValue() => item.IsTruthy();

        /// <summary>The value as a number, for liquid's arithmetic and its order against a number — the number made
        /// from it by number's own door; a value with no number in it is 0.</summary>
        public override decimal ToNumberValue()
            => global::app.type.item.number.@this.Create(item) is { } number ? number.Clr<decimal>() : 0m;

        /// <summary>Equal by the value's own Compare: a plang value against another, a liquid literal made the
        /// value the render's context makes of it.</summary>
        public override bool Equals(FluidValue other)
        {
            var that = other is Item plang ? plang.Held
                : other.IsNil() ? context.Null().Peek()
                : global::app.type.item.@this.Create(other.ToObjectValue(), context);
            var compared = item.Compare(that, context);
            // completed: a comparison that failed re-raises its own error; one still waiting on I/O is refused
            if (!compared.IsCompleted) throw new AppException(new global::app.error.@this(
                "a template compares a value whose comparison waits on I/O — read it first", "TemplateValueUnread", 400));
            return compared.GetAwaiter().GetResult() == global::app.data.Comparison.Equal;
        }

        /// <summary>The value as liquid's own sort, uniq and compare take it — Fluid is the third-party boundary: a
        /// leaf its raw backing (a string, a number), anything else the value itself.</summary>
        public override object ToObjectValue() => item.IsLeaf ? item.Backing! : item;

        // The plang value this answers for — whole, for a filter that writes it through plang's own format.
        internal global::app.type.item.@this Held => item;

        // The item navigated from — its own Data, with the render's context.
        private global::app.data.@this Parent => new("", item, context: context);
    }

    /// <summary>
    /// Heuristic: a string looks like a file path if it contains a dot-extension
    /// and no Liquid syntax markers. Used only when IsFile is null (auto-detect).
    /// </summary>
    private bool IsPath(string template)
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
    /// The folder a template's includes resolve from: the calling goal's, or the app's root.
    /// </summary>
    private global::app.type.item.path.@this Folder(Render action)
    {
        var context = action.Context;
        var goalPath = context.call.Goal?.Path;
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
    private async ValueTask<Completion> CallGoal(
        global::Fluid.Ast.Expression expression, TextWriter writer, System.Text.Encodings.Web.TextEncoder encoder,
        TemplateContext template)
    {
        var app = (app.@this)template.AmbientValues["app"];
        var context = (global::app.actor.context.@this)template.AmbientValues["context"];

        // A failed call fails the render — the render's result is the error, never an
        // "[Error: …]" printed into output that reads as success.
        var name = (await expression.EvaluateAsync(template)).ToStringValue();
        if (string.IsNullOrEmpty(name))
            throw new global::app.error.AppException("callGoal requires a goal name", "MissingGoalName", 400);

        var data = await app.goal.list.Find(name);
        if (!data.Success) throw new global::app.error.AppException(data.Error!);
        if (data.Peek() is not global::app.goal.@this goal)
            throw new global::app.error.GoalNotFoundException(name);

        var result = await goal.Start(context);
        if (!result.Success) throw new global::app.error.AppException(result.Error!);

        // the goal's answer writes itself as the template writes any value
        await FluidValue.Create(await result.Value(), template.Options).WriteToAsync(writer, encoder, template.CultureInfo);
        return Completion.Normal;
    }

    // --- Microsoft.Extensions.FileProviders adapter for Fluid's include/render tags ---

    // The includes a render reads, read before it: Fluid asks its file provider synchronously, so each
    // include the template names — and each one those name — is read first, through the gate, and the
    // provider answers from memory. An include named by a variable isn't known before the render: the
    // provider answers it with an error naming it.
    private sealed class PlangFileProvider : IFileProvider
    {
        // An include's name with this extension, added when it has none, is its key: Fluid asks for a name as written
        // and with the extension, and both find the one include.
        private const string Extension = ".liquid";
        private readonly global::app.type.item.path.@this _folder;
        private readonly global::app.actor.context.@this _context;
        // Every include named by a literal, by its key: its text, or null when it isn't there. A name with no key
        // here was named by a variable.
        private readonly Dictionary<string, string?> _include = new(StringComparer.OrdinalIgnoreCase);

        public PlangFileProvider(global::app.type.item.path.@this folder, global::app.actor.context.@this context)
        {
            _folder = folder;
            _context = context;
        }

        private string Key(string name)
            => name.EndsWith(Extension, StringComparison.OrdinalIgnoreCase) ? name : name + Extension;

        /// <summary>Reads every include <paramref name="template"/> names, and the includes they name.</summary>
        public async Task Load(IFluidTemplate template, FluidParser parser)
        {
            var pending = new Queue<IFluidTemplate>();
            pending.Enqueue(template);
            while (pending.TryDequeue(out var next))
                foreach (var name in new Included().In(next))
                {
                    var key = Key(name);
                    if (_include.ContainsKey(key)) continue;
                    var text = _include[key] = await Text(key);
                    if (text != null && parser.TryParse(text, out var partial, out _)) pending.Enqueue(partial);
                }
        }

        // The include's text — its key, or the name without the added extension; null when neither is there. The
        // probe and the read go through AuthGate(Read): an out-of-root include (`{% include '../../etc/passwd' %}`)
        // is a prompt or a denial, never a silent read, and a failed probe or read fails the render; only "absent"
        // is not found. An include is UTF-8 text, whatever its extension.
        private async Task<string?> Text(string key)
        {
            foreach (var name in new[] { key, key[..^Extension.Length] })
            {
                if (string.IsNullOrEmpty(name)) continue;
                var path = _folder.Combine(name);
                var exists = await path.Exists(_context);
                if (!exists.Success) throw new global::app.error.AppException(exists.Error!);
                if (!await exists.ToBooleanAsync()) continue;
                var bytes = await path.Bytes(_context);
                if (!bytes.Success) throw new global::app.error.AppException(bytes.Error!);
                return System.Text.Encoding.UTF8.GetString((await bytes.Value())?.Value ?? []);
            }
            return null;
        }

        public IFileInfo GetFileInfo(string subpath)
            => !_include.TryGetValue(Key(subpath), out var text)
                ? throw new global::app.error.AppException(
                    $"include '{subpath}' is named by a variable — an include is read before the render, so write its name out",
                    "IncludeNotLiteral", 400)
                : text is null ? new NotFoundFileInfo(subpath) : new Include(subpath, text);

        public IDirectoryContents GetDirectoryContents(string subpath)
            => new NotFoundDirectoryContents();

        public IChangeToken Watch(string filter)
            => NullChangeToken.Singleton;
    }

    // The include and render tags a template names by a literal, wherever they sit in it.
    private sealed class Included : AstVisitor
    {
        private readonly List<string> _names = new();

        public IReadOnlyList<string> In(IFluidTemplate template)
        {
            if (template is global::Fluid.Parser.FluidTemplate parsed)
                foreach (var statement in parsed.Statements) Visit(statement);
            return _names;
        }

        protected override Statement VisitIncludeStatement(IncludeStatement include)
        {
            if (include.Path is LiteralExpression literal) _names.Add(literal.Value.ToStringValue());
            return base.VisitIncludeStatement(include);
        }

        protected override Statement VisitRenderStatement(RenderStatement render)
        {
            _names.Add(render.Path);
            return base.VisitRenderStatement(render);
        }
    }

    // An include read before the render.
    private sealed class Include : IFileInfo
    {
        private readonly byte[] _content;

        public Include(string name, string text)
        {
            Name = name;
            _content = System.Text.Encoding.UTF8.GetBytes(text);
        }

        public bool Exists => true;
        public long Length => _content.Length;
        public string? PhysicalPath => null;
        public string Name { get; }
        public DateTimeOffset LastModified => DateTimeOffset.UtcNow;
        public bool IsDirectory => false;
        public Stream CreateReadStream() => new MemoryStream(_content, writable: false);
    }
}
