using PLang.Errors;
using PLang.Errors.Runtime;
using PLang.Runtime2.Engine.Context;
using PLang.Runtime2.Engine.Memory;
using PLang.Runtime2.Engine.Utility;
using System.Reflection;
namespace PLang.Runtime2.Engine.Goals.Goal.Steps.Step.Actions;

public sealed class @this : List<Action.@this>
{
    private readonly object? _context;

    public @this() { }
    public @this(object context) { _context = context; }
    public @this(IEnumerable<Action.@this> actions) : base(actions) { }

    [System.Text.Json.Serialization.JsonIgnore]
    public Step.@this? Step { get; set; }

    public new Action.@this this[int index]
    {
        get { var a = base[index]; a.Step ??= Step; return a; }
        set => base[index] = value;
    }

    public new IEnumerator<Action.@this> GetEnumerator()
    {
        for (int i = 0; i < Count; i++)
            yield return this[i];
    }

    public List<Action.@this> Value => this;

    public async Task<(string?, IError?)> Summary()
    {
        if (_context is PLang.Interfaces.PLangContext plangContext)
        {
            var template = plangContext.Engine.GetProgram<
                PLang.Modules.TemplateEngineModule.Program>();
            var vars = new Dictionary<string, object?> { ["actions"] = BuildTemplateData() };
            var result = await template.RenderFile(
                "/system/actions/summary.md", vars);
			return result;

		}
        return ("", new ProgramError("context is the right type"));
    }

    private List<object> BuildTemplateData()
    {
        var nCtx = new NullabilityInfoContext();
        return this.Select(a => (object)new
        {
            module = a.Module,
            action = a.ActionName,
            params_text = a.ParameterSchema == null ? null : string.Join(", ",
                a.ParameterSchema
                    .GetProperties(BindingFlags.Public | BindingFlags.Instance)
                    .Where(p => p.Name != "EqualityContract")
                    .Select(p =>
                    {
                        var nullable = nCtx.Create(p).WriteState == NullabilityState.Nullable;
                        var typeName = TypeMapping.GetTypeName(p.PropertyType);
                        return nullable ? $"{p.Name}?: {typeName}" : $"{p.Name}: {typeName}";
                    }))
        }).ToList();
    }

    public Task<Data> Load(PLangContext context)
    {
        return Task.FromResult(Data.Ok());
    }

    public async Task<Data> RunAsync(Engine.@this engine, PLangContext context, CancellationToken ct = default)
    {
        Data result = Data.Ok();
        foreach (var action in this)
        {
            result = await action.RunAsync(engine, context, ct);
            if (!result.Success) return result;
        }
        return result;
    }
}
