namespace app.type.item;

/// <summary>
/// A type whose value is its bytes: born from bytes it holds them as they are, and every format of the type writes
/// them back as they are (binary, image; text, as the characters they spell). So content of the type, not read yet,
/// is written to one of its formats by pouring its bytes as they stream — what reading it and writing it would give.
/// </summary>
internal interface IVerbatim;
