using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.Json;
using app.Utils;
using ActionSpec = app.builder.Types.Spec.Action;
using ExampleSpec = app.builder.Types.Spec.Example;

namespace app.builder.Types;

/// <summary>
/// Renders an <see cref="Example"/> into the formal-language string the
/// catalog "e.g. ..." line carries. Type tags ([path], [string], [list&lt;action&gt;])
/// are derived from reflection on each action class — the author never writes them.
///
/// Top-level peer actions and modifiers both use " | " as separator (matching the
/// existing Example convention in <c>system/actions/v2/summary.md</c> and the
/// goalFormatForLlm template). Nested action values (parameters typed
/// <c>action</c> / <c>list&lt;action&gt;</c>) emit as JSON action records — same
/// shape the LLM produces in its response.
///
/// Instance methods navigate <c>_modules</c> for type lookups; callers stop
/// passing modules in (Rule E).
/// </summary>
public sealed partial class @this
{
    public string Render(ExampleSpec spec)
    {
        var sb = new StringBuilder();
        for (int i = 0; i < spec.Chain.Length; i++)
        {
            if (i > 0) sb.Append(" | ");
            RenderActionFormal(spec.Chain[i], sb);
        }
        return sb.ToString();
    }

    private void RenderActionFormal(ActionSpec a, StringBuilder sb)
    {
        sb.Append(a.Module).Append('.').Append(a.Name);
        var actionType = _modules.GetActionType(a.Module, a.Name);

        if (a.Params.Count > 0)
        {
            sb.Append(' ');
            int i = 0;
            foreach (var (pname, pvalue) in a.Params)
            {
                if (i++ > 0) sb.Append(", ");
                var typeName = LookupParamTypeName(actionType, pname);
                sb.Append(pname).Append("([").Append(typeName).Append("] ");
                RenderValueFormal(pvalue, sb);
                sb.Append(')');
            }
        }

        if (a.Modifiers != null)
        {
            foreach (var mod in a.Modifiers)
            {
                sb.Append(" | ");
                RenderActionFormal(mod, sb);
            }
        }
    }

    /// <summary>
    /// Format a value the way the catalog Example does: %vars% bare, strings
    /// with spaces or commas quoted, scalars literal, Action(s) as
    /// nested-action JSON records.
    /// Mirrors <c>Fluid.FormatFormalValue</c> for the simple cases.
    /// </summary>
    private void RenderValueFormal(object? value, StringBuilder sb)
    {
        if (value is null) { sb.Append("null"); return; }

        if (value is string s)
        {
            if (s.StartsWith('%')) sb.Append(s);
            else if (s.Contains(' ') || s.Contains(',')) sb.Append('"').Append(s).Append('"');
            else sb.Append(s);
            return;
        }

        if (value is bool b) { sb.Append(b ? "true" : "false"); return; }

        if (value is ActionSpec one)
        {
            sb.Append(JsonSerializer.Serialize(BuildActionRecord(one)));
            return;
        }

        // Lists / arrays of Action — list<action> parameters
        if (value is IEnumerable enumerable && value is not string)
        {
            var items = enumerable.Cast<object?>().ToList();
            if (items.Count > 0 && items.All(x => x is ActionSpec))
            {
                var records = items.Select(x => BuildActionRecord((ActionSpec)x!)).ToList();
                sb.Append(JsonSerializer.Serialize(records));
                return;
            }
            // Other lists fall through to JSON below.
        }

        // InvariantCulture so JSON-shaped numbers ("3.14") format identically
        // regardless of the user's locale — without this, an it-IT/de-DE user
        // would write "3,14" to .pr files and TypeConverter.Convert.ChangeType
        // (which uses InvariantCulture on the parse side) would FormatException
        // on the comma. Round-trip must be symmetric.
        if (value is System.IConvertible conv) { sb.Append(System.Convert.ToString(conv, System.Globalization.CultureInfo.InvariantCulture)); return; }

        sb.Append(JsonSerializer.Serialize(value));
    }

    /// <summary>
    /// Builds the lower-cased JSON-record shape the LLM emits for nested actions:
    /// <c>{"module","action","parameters":[{"name","value","type"}]}</c>.
    /// Recurses into modifier and Action-valued parameters.
    /// </summary>
    private Dictionary<string, object?> BuildActionRecord(ActionSpec a)
    {
        var actionType = _modules.GetActionType(a.Module, a.Name);
        var record = new Dictionary<string, object?>
        {
            ["module"] = a.Module,
            ["action"] = a.Name,
        };

        if (a.Params.Count > 0)
        {
            var paramList = new List<Dictionary<string, object?>>();
            foreach (var (pname, pvalue) in a.Params)
            {
                paramList.Add(new Dictionary<string, object?>
                {
                    ["name"] = pname,
                    ["value"] = ConvertValueForJson(pvalue),
                    ["type"] = LookupParamTypeName(actionType, pname),
                });
            }
            record["parameters"] = paramList;
        }

        if (a.Modifiers != null && a.Modifiers.Length > 0)
        {
            record["modifiers"] = a.Modifiers
                .Select(m => BuildActionRecord(m))
                .ToList();
        }

        return record;
    }

    private object? ConvertValueForJson(object? value)
    {
        if (value is ActionSpec one) return BuildActionRecord(one);

        if (value is IEnumerable enumerable && value is not string)
        {
            var items = enumerable.Cast<object?>().ToList();
            if (items.Count > 0 && items.All(x => x is ActionSpec))
                return items.Select(x => BuildActionRecord((ActionSpec)x!)).ToList();
        }

        return value;
    }

    private string LookupParamTypeName(System.Type? actionType, string paramName)
    {
        if (actionType == null) return "object";
        var prop = actionType.GetProperty(paramName, BindingFlags.Public | BindingFlags.Instance);
        if (prop == null) return "object";
        var unwrapped = UnwrapDataAndNullable(prop.PropertyType);
        // Variable-name slots advertise as "string" — the LLM emits a name, not the
        // type-marker token. Same convention as Modules.@this.Describe().
        if (typeof(app.variable.IRawNameResolvable).IsAssignableFrom(unwrapped))
            return "string";
        return _modules.App?.Types.GetTypeName(unwrapped)
               ?? unwrapped.Name.ToLowerInvariant();
    }

    /// <summary>
    /// Catalog parameter types are stored as <c>Data&lt;T&gt;?</c>. The LLM sees the inner
    /// T's PLang name (e.g. <c>string</c>, <c>path</c>, <c>list&lt;action&gt;</c>), not "data" —
    /// matches the existing catalog rendering on Modules.@this.Describe().
    /// </summary>
    private static System.Type UnwrapDataAndNullable(System.Type t)
    {
        var underlying = System.Nullable.GetUnderlyingType(t);
        if (underlying != null) t = underlying;

        if (t.IsGenericType && t.GetGenericTypeDefinition() == typeof(data.@this<>))
            t = t.GetGenericArguments()[0];

        return t;
    }
}
