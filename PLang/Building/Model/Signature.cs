namespace PLang.Model;

public class SignatureInfo(string signature, Dictionary<string, object> keyValues)
{
    public string Signature { get; } = signature;
    public Dictionary<string, object> KeyValues { get; } = keyValues;
}