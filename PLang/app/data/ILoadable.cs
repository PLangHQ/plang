namespace app.data;

/// <summary>
/// A value whose content materialises through async I/O — a reference
/// fundamental (image; audio/video follow the same shape). <see cref="LoadAsync"/>
/// pulls the content into memory so the SYNC readers that run below the
/// serializer's STJ converter wall (the per-type renderers, <c>Width</c>/<c>Height</c>)
/// observe real bytes instead of an empty placeholder.
///
/// <para>Distinct from <see cref="IStrictKindEnforcer"/>: a lazy reference
/// fundamental needs loading whether or not it is strict — an unloaded handle
/// renders empty regardless. Strict enforcement piggybacks because the load seam
/// (<c>image.BytesAsync</c>) is also where <see cref="StrictKindMismatchException"/>
/// throws.</para>
///
/// <para>Implementations must be idempotent: a value already in memory returns
/// immediately with no I/O.</para>
/// </summary>
public interface ILoadable
{
    System.Threading.Tasks.Task LoadAsync();
}
