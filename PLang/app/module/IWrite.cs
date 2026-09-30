namespace app.module;

/// <summary>
/// An action that writes to a location — <c>file.save</c>, <c>file.copy</c>, <c>file.move</c>: the location it writes,
/// so the build knows a later step's read of it finds it there (a goal that saves a file, then reads it).
/// </summary>
public interface IWrite
{
    /// <summary>The location this action writes.</summary>
    data.@this<global::app.type.item.path.@this> Target { get; }
}
