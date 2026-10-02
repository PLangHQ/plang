namespace app.type.item.variable.code;

/// <summary>
/// A call on the value: <c>.replace("-", " ")</c>, <c>.grep("error", 2)</c>. The value owns its
/// methods — only the ones its type marks <c>[LlmBuilder]</c> are reachable from a variable, since a
/// variable only reads. The name matches without regard to case; each parameter was parsed at build
/// (a literal, or a variable resolved at run) and is made the type the method asks for.
/// </summary>
public sealed class Method : Hop
{
    public string Name { get; }

    /// <summary>The values handed to the method, in order: literals, or variables.</summary>
    public global::app.type.item.list.@this Parameter { get; }

    internal Method(string text, string name, global::app.type.item.list.@this parameter) : base(text)
    {
        Name = name;
        Parameter = parameter;
    }

    public override string Kind => "method";

    /// <summary>A call on the value is own when every value handed to it is — a literal holds no variable.</summary>
    internal override bool IsOwn => Parameter.Slots().Cast<global::app.type.item.@this>()
        .SelectMany(value => value.Variable).All(v => v.IsOwn);

    /// <summary>Its name, then its values as rows: <c>"replace", "parameter": [{"type": {"name": "text"},
    /// "value": "-"}, …]</c>; a variable's row names it, as every stored row does.</summary>
    protected override void Piece(global::app.type.format.IWriter writer)
    {
        writer.String(Name);
        writer.Name("parameter");
        var values = Parameter.Slots().Cast<global::app.type.item.@this>().ToList();
        writer.BeginArray(values.Count);
        foreach (var value in values)
        {
            writer.BeginObject();
            writer.Name("type");
            value.Type.Write(writer);
            writer.Name("value");
            value.Write(writer);
            if (value.HasVariable)
            {
                writer.Name("variable");
                new variable.serializer.Entry().Write(writer, value.Variable);
            }
            writer.EndObject();
        }
        writer.EndArray();
    }

    public override async System.Threading.Tasks.ValueTask<global::app.data.@this> Start(
        global::app.data.@this? previous, global::app.actor.context.@this context)
    {
        if (previous is null) return context.NotFound(Text);
        var target = await previous.Value();
        if (!previous.Success) return previous;

        var given = Parameter.Items(context).ToList();
        // a method takes the values given in order; the ones left out at its end are those it says are optional
        var method = target.GetType().GetMethods(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance)
            .FirstOrDefault(m => string.Equals(m.Name, Name, System.StringComparison.OrdinalIgnoreCase)
                && System.Attribute.IsDefined(m, typeof(global::app.LlmBuilderAttribute))
                && m.GetParameters().Where(p => p.ParameterType != typeof(global::app.actor.context.@this)).ToList() is var takes
                && takes.Count >= given.Count && takes.Skip(given.Count).All(p => p.IsOptional));
        if (method == null)
            return context.Error(new global::app.error.Error(
                $"{target.Type.Name} has no method '{Name}' taking {given.Count} value{(given.Count == 1 ? "" : "s")}.",
                "MethodNotFound", 400));

        var args = new List<object?>();
        int at = 0;
        foreach (var p in method.GetParameters())
        {
            if (p.ParameterType == typeof(global::app.actor.context.@this)) { args.Add(context); continue; }
            if (at >= given.Count) { args.Add(p.DefaultValue); continue; }
            var row = given[at++];
            var item = await row.Value();
            if (!row.Success) return row;
            if (p.ParameterType.IsInstanceOfType(item)) { args.Add(item); continue; }
            var made = context.App.type.list[p.ParameterType].Make(item, row);
            if (made is null) return context.Error(row.Error!);
            args.Add(made);
        }

        var answer = method.Invoke(target, args.ToArray());
        if (answer is System.Threading.Tasks.Task task)
        {
            await task;
            answer = task.GetType().GetProperty("Result")?.GetValue(task);
        }
        return answer as global::app.data.@this ?? new global::app.data.@this(previous.Name, answer, context: context);
    }
}
