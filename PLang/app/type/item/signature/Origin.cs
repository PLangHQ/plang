namespace app.type.item.signature;

/// <summary>Where a signature came from — born with it, deciding how it is judged.</summary>
[global::app.Attributes.PlangType("origin")]
public enum Origin
{
    /// <summary>Just signed, here: never read. Its hash is the view it was signed in (Out); no window, no nonce check.</summary>
    Signed,

    /// <summary>Read live off the wire: good for its window after it was made, its nonce presented once.</summary>
    Live,

    /// <summary>Read from plang's own store: what it covers is the value's stored form; no window, its nonce re-presents
    /// on every read.</summary>
    Stored,
}
