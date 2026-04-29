using System.Reflection;
using System.Text;
using System.Text.Json.Serialization;
using App.Attributes;
using App.Utils;

namespace App.Catalog;

/// <summary>
/// The structured type catalog the builder publishes to the LLM. Holds the
/// primitive names and the record/enum entries discovered from action parameters.
/// Consumers: the builder prompt template (Liquid reads <see cref="TypeNames"/>
/// and <see cref="TypeSchemas"/>), the trace viewer, future tooling.
///
/// OBP: the catalog is a real object. You can hold it, pass it around, JSON it,
/// or ask it to render itself. It owns its schema.
/// </summary>
[PlangType("catalog")]
public sealed class @this
{
    /// <summary>Ordered list of primitive type names exposed to the builder.</summary>
    [LlmBuilder]
    public IReadOnlyList<string> PrimitiveNames { get; init; } = System.Array.Empty<string>();

    /// <summary>Record and enum entries referenced by the action catalog.</summary>
    [LlmBuilder]
    public IReadOnlyList<TypeEntry> Types { get; init; } = System.Array.Empty<TypeEntry>();

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
                if (t.Kind == TypeKind.Enum && t.Values != null)
                {
                    sb.Append(string.Join(" | ", t.Values));
                }
                else if (t.Kind == TypeKind.Record && t.Fields != null)
                {
                    sb.Append("{ ");
                    for (int i = 0; i < t.Fields.Count; i++)
                    {
                        if (i > 0) sb.Append(", ");
                        sb.Append(t.Fields[i].Name).Append(": ").Append(t.Fields[i].TypeName);
                    }
                    sb.Append(" }");
                }
                else if (t.Kind == TypeKind.Scalar)
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
    /// Builds a Catalog by walking the modules' action parameter types. Discovery is
    /// transitive: every type referenced in a schema is itself surfaced.
    /// The @this decision (OBP types are catalog-visible by convention), [PlangType]
    /// resolution, enum handling, and [LlmBuilder]-filtered fields all live in
    /// TypeMapping — Catalog.Build just assembles the result.
    /// </summary>
    public static @this Build(App.Modules.@this? modules)
    {
        var primitives = TypeMapping.GetBuilderTypeNames();
        var types = TypeMapping.BuildTypeEntries(modules);

        return new @this
        {
            PrimitiveNames = primitives,
            Types = types,
        };
    }

    /// <summary>
    /// Serializes the catalog as JSON — structured Types + PrimitiveNames, with
    /// camelCase keys. ClrType is hidden (tagged JsonIgnore on TypeEntry). The
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
        // TypeKind emits as "Record" / "Enum" — more useful than the numeric default.
        options.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter());
        return System.Text.Json.JsonSerializer.Serialize(this, options);
    }
}
