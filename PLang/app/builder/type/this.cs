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
///     and exposes <see cref="Build"/>. Its <see cref="PrimitiveNames"/> /
///     <see cref="Types"/> are empty arrays.
///   - Built result (returned by <see cref="Build"/>) — same instance shape,
///     populated. Consumers (builder Liquid template, trace viewer) read the
///     strongly-typed fields directly; the template owns the rendering.
///
/// OBP: schema is a real object owned by Modules. Reach it via
/// <c>app.builder.type</c>; build a snapshot via
/// <c>app.builder.type.Build()</c>. Rendering belongs in the template, not
/// in pre-rendered string properties.
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

    /// <summary>
    /// Builds a fresh Schema by walking <c>_modules</c>' action parameter types.
    /// Discovery is transitive: every type referenced in a schema is itself surfaced.
    /// </summary>
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

}
