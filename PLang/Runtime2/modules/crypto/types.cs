namespace PLang.Runtime2.modules.crypto;

public class HashedData
{
    public string Algorithm { get; set; } = "";
    public string Format { get; set; } = "";
    public string Hash { get; set; } = "";
    public override string ToString() => Hash;
}
