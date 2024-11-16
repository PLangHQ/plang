namespace PLang.Exceptions;

public class SettingsException : Exception
{
    public object DefaultValue;
    public string Explain;
    public string Key;

    public SettingsException(string key, object defaultValue, string explain)
    {
        Key = key;
        Explain = explain;
        DefaultValue = defaultValue;
    }
}

public class InvalidSettingsException : SettingsException
{
    public InvalidSettingsException(string key, object defaultValue, string explain) : base(key, defaultValue, explain)
    {
    }
}