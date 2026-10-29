namespace app.snapshot;

/// <summary>
/// A value written and read in a view of its own: its class states it. A value signed in it is hashed there, and read
/// back at rest wherever it is carried (a snapshot's file is no message on the wire).
/// </summary>
public interface IView<TSelf> where TSelf : global::app.type.item.@this, IView<TSelf>
{
    /// <summary>The view a value of this class is written and read in.</summary>
    static abstract global::app.View View { get; }
}
