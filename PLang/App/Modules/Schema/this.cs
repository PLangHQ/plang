using System.Text;
using System.Text.Json.Serialization;
using App.Attributes;
using App.Utils;

namespace App.Modules.Schema;

/// <summary>
/// "What every action looks like, for the LLM." Owned by Modules; describes
/// the registered actions' types, parameter schemas, and authored Examples.
///
/// Two roles in one type:
///   - Host (the instance held at <c>app.Modules.Schema</c>) — has <c>_modules</c>
///     and exposes <see cref="Build"/> + <see cref="Render"/>. Its
///     <see cref="PrimitiveNames"/> / <see cref="Types"/> are empty arrays.
///   - Built result (returned by <see cref="Build"/>) — same instance shape,
///     populated PrimitiveNames + Types. Consumers (builder template, trace
///     viewer) use the built result.
///
/// OBP: schema is a real object owned by Modules. Reach it via
/// <c>app.Modules.Schema</c>; build a snapshot via
/// <c>app.Modules.Schema.Build()</c>.
/// </summary>
[PlangType("catalog")]
public sealed partial class @this
{
    private readonly App.Modules.@this _modules;

    public @this(App.Modules.@this modules) { _modules = modules; }

    /// <summary>Ordered list of primitive type names exposed to the builder.</summary>
    [LlmBuilder]
    public IReadOnlyList<string> PrimitiveNames { get; init; } = System.Array.Empty<string>();

    /// <summary>Record and enum entries referenced by the action catalog.</summary>
    [LlmBuilder]
    public IReadOnlyList<Entry> Types { get; init; } = System.Array.Empty<Entry>();

    // ---- Template conveniences (pre-rendered views the Liquid prompt consumes) ----

    /// <summary>Comma-joined primitive names — the string the builder template drops in.</summary>
    [JsonIgnore]
    public string TypeNames => string.Join(", ", PrimitiveNames);

    /// <summary>
    /// The full schema block rendered as the markdown shape the builder prompt expects:
    ///   `name: v1 | v2 | ...`           for Enum entries
    ///   `name: { k: T, ... }`            for Record entries
    /// Pre-rendered so the Liquid template stays a dumb stitcher.
    /// </summary>
    [JsonIgnore]
    public string TypeSchemas
    {
        get
        {
            var sb = new StringBuilder();
            foreach (var t in Types)
            {
                sb.Append("  ").Append(t.Name).Append(": ");
                if (t.Kind == EntryKind.Enum && t.Values != null)
                {
                    sb.Append(string.Join(" | ", t.Values));
                }
                else if (t.Kind == EntryKind.Record && t.Fields != null)
                {
                    sb.Append("{ ");
                    for (int i = 0; i < t.Fields.Count; i++)
                    {
                        if (i > 0) sb.Append(", ");
                        sb.Append(t.Fields[i].Name).Append(": ").Append(t.Fields[i].TypeName);
                    }
                    sb.Append(" }");
                }
                else if (t.Kind == EntryKind.Scalar)
                {
                    if (t.ConstructorSignature != null)
                        sb.Append("constructor(").Append(t.ConstructorSignature).Append(')');
                    else if (t.Shape != null)
                        sb.Append(t.Shape);
                    if (t.Properties != null && t.Properties.Count > 0)
                    {
                        sb.Append(", properties: ");
                        for (int i = 0; i < t.Properties.Count; i++)
                        {
                            if (i > 0) sb.Append(", ");
                            sb.Append(t.Properties[i].Name)
                              .Append('(').Append(t.Properties[i].TypeName).Append(')');
                        }
                    }
                }
                if (!string.IsNullOrEmpty(t.Description))
                    sb.Append(" — ").Append(t.Description);
                if (!string.IsNullOrEmpty(t.Example))
                    sb.Append(" (e.g. ").Append(t.Example).Append(')');
                sb.AppendLine();
            }
            return sb.ToString().TrimEnd();
        }
    }

    /// <summary>
    /// Builds a fresh Schema by walking <c>_modules</c>' action parameter types.
    /// Discovery is transitive: every type referenced in a schema is itself surfaced.
    /// The @this decision (OBP types are catalog-visible by convention), [PlangType]
    /// resolution, enum handling, and [LlmBuilder]-filtered fields all live in
    /// TypeMapping — Build just assembles the result.
    /// </summary>
    public @this Build()
    {
        var primitives = _modules.App?.Types.GetBuilderTypeNames() ?? new List<string>();
        var types = _modules.App?.Types.BuildTypeEntries(_modules) ?? new List<Entry>();

        return new @this(_modules)
        {
            PrimitiveNames = primitives,
            Types = types,
        };
    }

    /// <summary>
    /// Serializes the schema as JSON — structured Types + PrimitiveNames, with
    /// camelCase keys. ClrType is hidden (tagged JsonIgnore on Entry). The
    /// result is safe to expose to docs/UI/trace-viewer consumers.
    /// </summary>
    public string ToJson(bool indent = true)
    {
        var options = new System.Text.Json.JsonSerializerOptions
        {
            WriteIndented = indent,
            PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
        };
        // EntryKind emits as "Record" / "Enum" — more useful than the numeric default.
        options.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter());
        return System.Text.Json.JsonSerializer.Serialize(this, options);
    }
}
