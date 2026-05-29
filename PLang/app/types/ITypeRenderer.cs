namespace app.types;

/// <summary>
/// Type-system analogue of <see cref="app.modules.code.ICode"/> for
/// runtime-loaded DLLs that ship per-(type, format) renderers. A loaded
/// assembly exposes one <see cref="ITypeRenderer"/> instance per format
/// it supports; the loader registers each via
/// <see cref="app.types.renderers.@this.Register"/>.
///
/// <para>The interface intentionally mirrors the shape of the static
/// <c>app/types/&lt;name&gt;/serializer/&lt;format&gt;.cs</c> files that
/// the generator-emitted dispatch wires at build — <see cref="Format"/>
/// is the format token (<c>"json"</c>, <c>"plang"</c>, <c>"*"</c>, …),
/// <see cref="Write"/> is the rendering. <see cref="TypeName"/> tells the
/// loader which PLang type the renderer belongs to (a single DLL can ship
/// renderers for several types).</para>
/// </summary>
public interface ITypeRenderer
{
    /// <summary>PLang type name the renderer is for (e.g. <c>"image"</c>).</summary>
    string TypeName { get; }

    /// <summary>Format token (e.g. <c>"json"</c>, <c>"protobuf"</c>, <c>"*"</c> for wildcard).</summary>
    string Format { get; }

    /// <summary>Renders <paramref name="value"/> via <paramref name="writer"/>'s primitive surface.</summary>
    void Write(object value, app.channel.serializer.IWriter writer);
}
