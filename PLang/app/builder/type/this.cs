using System.Text;
using System.Text.Json.Serialization;
using app.Attributes;
using app.Utils;

namespace app.builder.type;

/// <summary>
/// "What every action looks like, for the LLM." Owned by Modules; describes
/// the registered actions' types, parameter schemas, and authored Examples.
///
/// Two roles in one type:
///   - Host (the instance held at <c>app.builder.type</c>) — has <c>_modules</c>
///     and exposes <see cref="Build"/> + <see cref="Render"/>. Its
///     <see cref="PrimitiveNames"/> / <see cref="Types"/> are empty arrays.
///   - Built result (returned by <see cref="Build"/>) — same instance shape,
///     populated PrimitiveNames + Types. Consumers (builder template, trace
///     viewer) use the built result.
///
/// OBP: schema is a real object owned by Modules. Reach it via
/// <c>app.builder.type</c>; build a snapshot via
/// <c>app.builder.type.Build()</c>.
/// </summary>
public sealed partial class @this
{
    private readonly app.module.@this _modules;

    public @this(app.module.@this modules) { _modules = modules; }

    /// <summary>Ordered list of primitive type names exposed to the builder.</summary>
    [LlmBuilder]
    public IReadOnlyList<string> PrimitiveNames { get; init; } = System.Array.Empty<string>();

    /// <summary>Record and enum entries referenced by the action catalog.</summary>
    [LlmBuilder]
    public IReadOnlyList<global::app.type.@this> Types { get; init; } = System.Array.Empty<global::app.type.@this>();

    /// <summary>
    /// Per-family kind vocabulary the LLM may emit for the <c>type</c> parameter.
    /// Inverted from the format registry's extension→family map — e.g.
    /// <c>image → [jpg, jpeg, png, gif, ...]</c>, <c>text → [txt, json, csv,
    /// md, ...]</c>. Numerics (kinds of <c>number</c>) are added explicitly
    /// since they aren't extensions. Stable, catalog-derived — teaches the
    /// LLM what (name, kind) combos are valid without a per-action override.
    /// </summary>
    [LlmBuilder]
    public IReadOnlyDictionary<string, IReadOnlyList<string>> Kinds { get; init; }
        = new Dictionary<string, IReadOnlyList<string>>();

    // ---- Template conveniences (pre-rendered views the Liquid prompt consumes) ----

    /// <summary>Comma-joined primitive names — the string the builder template drops in.</summary>
    [JsonIgnore]
    public string TypeNames => string.Join(", ", PrimitiveNames);

    /// <summary>
    /// Pre-rendered LLM teaching for the <c>type</c> parameter shape and the
    /// kind vocabulary per name. The compiler prompt drops this in when an
    /// action declares a <c>type</c>-shaped parameter (today: <c>variable.set</c>).
    /// </summary>
    [JsonIgnore]
    public string KindsCatalog
    {
        get
        {
            if (Kinds.Count == 0) return "";
            var sb = new StringBuilder();
            sb.AppendLine("| name | kind |");
            sb.AppendLine("|---|---|");
            foreach (var name in Kinds.Keys.OrderBy(k => k))
            {
                var kinds = Kinds[name];
                if (kinds.Count == 0) continue;
                sb.Append("| ").Append(name).Append(" | ")
                  .Append(string.Join(", ", kinds)).AppendLine(" |");
            }
            return sb.ToString().TrimEnd();
        }
    }

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
                if (t.Values != null)
                {
                    sb.Append(string.Join(" | ", t.Values));
                }
                else if (t.Fields != null)
                {
                    sb.Append("{ ");
                    for (int i = 0; i < t.Fields.Count; i++)
                    {
                        if (i > 0) sb.Append(", ");
                        sb.Append(t.Fields[i].Name).Append(": ").Append(t.Fields[i].TypeName);
                    }
                    sb.Append(" }");
                }
                else
                {
                    // Scalar: emit just the constructor-input type, not the
                    // `constructor(name: type), properties: ...` verbose form.
                    // The verbose form misleads the LLM into emitting a record
                    // (dict) for parameters of this type; what it actually wants
                    // is a scalar matching the input form (e.g. a string for
                    // `path`). The dot-path properties (extension, exists, ...)
                    // stay accessible at runtime via type registration; they
                    // don't need to live in the catalog summary.
                    if (t.ConstructorSignature != null)
                    {
                        var sig = t.ConstructorSignature;
                        var colonIdx = sig.IndexOf(':');
                        sb.Append(colonIdx > 0 ? sig[(colonIdx + 1)..].Trim() : sig);
                    }
                    else if (t.Shape != null)
                    {
                        sb.Append(t.Shape);
                    }
                }
                // Dual-mode kind teaching: advertised (closed list) vs extension-derived (open).
                if (t.Kinds != null && t.Kinds.Count > 0)
                {
                    // Advertised vocabulary — closed set; the LLM picks one.
                    sb.Append(" — kinds: ").Append(string.Join(" | ", t.Kinds));
                }
                else if (HasBuildHook(t))
                {
                    // Extension-derived kind — the kind is the file extension.
                    sb.Append(" — kind = extension");
                    var examples = ExtensionExamples(t.Name);
                    if (examples != null) sb.Append(" (").Append(examples).Append(')');
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
    // Heuristic: does the type advertise a build-time kind hook via the
    // dispatcher? Mirrors what app.type.kind.@this.Discover looks for.
    private bool HasBuildHook(global::app.type.@this t)
    {
        var clr = t.ClrType;
        if (clr == null) return false;
        var m = clr.GetMethod("Build",
            System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static
            | System.Reflection.BindingFlags.FlattenHierarchy,
            binder: null,
            types: new[] { typeof(object) },
            modifiers: null);
        return m != null && m.ReturnType == typeof(string);
    }

    // Small, stable example list for extension-derived kinds; falls back to
    // null when we don't have curated examples (so the renderer omits the parens).
    private static string? ExtensionExamples(string typeName) => typeName switch
    {
        "text"  => "md, txt, csv, html, ...",
        "image" => "jpg, png, gif, webp, ...",
        "audio" => "mp3, wav, flac, ...",
        "video" => "mp4, webm, mov, ...",
        _ => null,
    };

    public @this Build()
    {
        var primitives = _modules.App?.Type.GetBuilderTypeNames() ?? new List<string>();
        var types = _modules.App?.Type.BuildTypeEntries(_modules) ?? new List<global::app.type.@this>();

        // Family → kind vocabulary, inverted from the format registry's
        // extension→family map. number's kinds aren't extensions, so we add
        // them explicitly — the source of truth is app.type.number.@this.Kinds.
        var kindsByName = new Dictionary<string, IReadOnlyList<string>>(System.StringComparer.OrdinalIgnoreCase);
        if (_modules.App?.Format is { } fmt)
        {
            foreach (var kvp in fmt.KindsByFamily())
                kindsByName[kvp.Key] = kvp.Value;
        }
        kindsByName["number"] = new[] { "int", "long", "decimal", "double" };

        return new @this(_modules)
        {
            PrimitiveNames = primitives,
            Types = types,
            Kinds = kindsByName,
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
        return System.Text.Json.JsonSerializer.Serialize(this, options);
    }
}
