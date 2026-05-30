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

        // name → kind vocabulary the LLM may emit. Two sources:
        //   - Format registry's extension→family map (text, image, audio, …) —
        //     the file-extension kinds.
        //   - Types that ADVERTISE a static `Kinds` vocabulary (number's
        //     precisions, hash's algorithms) — not extension-derived. Pulled
        //     from each catalog entry's folded Kinds so the type owns its list
        //     (no hardcoding here; add a type with a static Kinds and it shows).
        var kindsByName = new Dictionary<string, IReadOnlyList<string>>(System.StringComparer.OrdinalIgnoreCase);
        if (_modules.App?.Format is { } fmt)
        {
            foreach (var kvp in fmt.KindsByFamily())
                kindsByName[kvp.Key] = kvp.Value;
        }
        // Advertised-kind types (number's precisions, hash's algorithms) are
        // surfaced even when they're only a return type (not a step param), so
        // draw from ALL known types — any with a static Kinds advertises here.
        var allKnown = _modules.App?.Type.BuildTypeEntries(null) ?? new List<global::app.type.@this>();
        foreach (var t in allKnown)
            if (t.Kinds is { Count: > 0 })
                kindsByName[t.Name] = t.Kinds;

        return new @this(_modules)
        {
            PrimitiveNames = primitives,
            Types = types,
            Kinds = kindsByName,
        };
    }

}
