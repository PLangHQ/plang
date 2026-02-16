using PLang.Errors;
using PLang.Errors.Runtime;
using PLang.Runtime2.Context;
using PLang.Runtime2.Memory;
using PLang.Runtime2.Utility;
using System.Reflection;

namespace PLang.Runtime2;

public sealed class Actions : List<Action>
{
    private readonly object? _context;

    public Actions() { }
    public Actions(object context) { _context = context; }
    public Actions(IEnumerable<Action> actions) : base(actions) { }

    public List<Action> Value => this;

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

    public async Task<Data> RunAsync(Engine engine, PLangContext context, CancellationToken ct = default)
    {
        Data merged = Data.Ok();
        foreach (var action in this)
        {
            var result = await action.RunAsync(engine, context, ct);
            if (!result.Success) return result;
            merged = merged.Merge(result);
        }
        return merged;
    }
}
