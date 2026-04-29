using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.Json;
using App.Utils;

namespace App.Catalog;

/// <summary>
/// Renders an <see cref="ExampleSpec"/> into the formal-language string the
/// catalog "e.g. ..." line carries. Type tags ([path], [string], [list&lt;action&gt;])
/// are derived from reflection on each action class — the author never writes them.
///
/// Top-level peer actions and modifiers both use " | " as separator (matching the
/// existing Example convention in <c>system/actions/v2/summary.md</c> and the
/// goalFormatForLlm template). Nested action values (parameters typed
/// <c>action</c> / <c>list&lt;action&gt;</c>) emit as JSON action records — same
/// shape the LLM produces in its response.
/// </summary>
public static class ExampleRenderer
{
    public static string Render(ExampleSpec spec, App.Modules.@this modules)
    {
        var sb = new StringBuilder();
        for (int i = 0; i < spec.Chain.Length; i++)
        {
            if (i > 0) sb.Append(" | ");
            RenderActionFormal(spec.Chain[i], modules, sb);
        }
        return sb.ToString();
    }

    private static void RenderActionFormal(ActionSpec a, App.Modules.@this modules, StringBuilder sb)
    {
        sb.Append(a.Module).Append('.').Append(a.Name);
        var actionType = modules.GetActionType(a.Module, a.Name);

        if (a.Params.Count > 0)
        {
            sb.Append(' ');
            int i = 0;
            foreach (var (pname, pvalue) in a.Params)
            {
                if (i++ > 0) sb.Append(", ");
                var typeName = LookupParamTypeName(actionType, pname);
                sb.Append(pname).Append("([").Append(typeName).Append("] ");
                RenderValueFormal(pvalue, modules, sb);
                sb.Append(')');
            }
        }

        if (a.Modifiers != null)
        {
            foreach (var mod in a.Modifiers)
            {
                sb.Append(" | ");
                RenderActionFormal(mod, modules, sb);
            }
        }
    }

    /// <summary>
    /// Format a value the way the catalog Example does: %vars% bare, strings
    /// with spaces or commas quoted, scalars literal, ActionSpec(s) as
    /// nested-action JSON records.
    /// Mirrors <c>FluidProvider.FormatFormalValue</c> for the simple cases.
    /// </summary>
    private static void RenderValueFormal(object? value, App.Modules.@this modules, StringBuilder sb)
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
            sb.Append(JsonSerializer.Serialize(BuildActionRecord(one, modules)));
            return;
        }

        // Lists / arrays of ActionSpec — list<action> parameters
        if (value is IEnumerable enumerable && value is not string)
        {
            var items = enumerable.Cast<object?>().ToList();
            if (items.Count > 0 && items.All(x => x is ActionSpec))
            {
                var records = items.Select(x => BuildActionRecord((ActionSpec)x!, modules)).ToList();
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
    /// Recurses into modifier and ActionSpec-valued parameters.
    /// </summary>
    private static Dictionary<string, object?> BuildActionRecord(ActionSpec a, App.Modules.@this modules)
    {
        var actionType = modules.GetActionType(a.Module, a.Name);
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
                    ["value"] = ConvertValueForJson(pvalue, modules),
                    ["type"] = LookupParamTypeName(actionType, pname),
                });
            }
            record["parameters"] = paramList;
        }

        if (a.Modifiers != null && a.Modifiers.Length > 0)
        {
            record["modifiers"] = a.Modifiers
                .Select(m => BuildActionRecord(m, modules))
                .ToList();
        }

        return record;
    }

    private static object? ConvertValueForJson(object? value, App.Modules.@this modules)
    {
        if (value is ActionSpec one) return BuildActionRecord(one, modules);

        if (value is IEnumerable enumerable && value is not string)
        {
            var items = enumerable.Cast<object?>().ToList();
            if (items.Count > 0 && items.All(x => x is ActionSpec))
                return items.Select(x => BuildActionRecord((ActionSpec)x!, modules)).ToList();
        }

        return value;
    }

    private static string LookupParamTypeName(System.Type? actionType, string paramName)
    {
        if (actionType == null) return "object";
        var prop = actionType.GetProperty(paramName, BindingFlags.Public | BindingFlags.Instance);
        if (prop == null) return "object";
        return TypeMapping.GetTypeName(UnwrapDataAndNullable(prop.PropertyType));
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

        if (t.IsGenericType && t.GetGenericTypeDefinition() == typeof(Data.@this<>))
            t = t.GetGenericArguments()[0];

        return t;
    }
}
