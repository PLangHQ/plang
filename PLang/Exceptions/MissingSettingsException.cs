using PLang.Errors.Handlers;

namespace PLang.Exceptions;

public class MissingSettingsException : AskUserError
{
    private readonly Type callingType;
    private readonly string key;
    private readonly string type;

    public MissingSettingsException(Type callingType, string type, string key, object defaultValue, string explain,
        Action<Type, string, string, object> callback) : base(explain, CreateAdapter(callback))
    {
        this.callingType = callingType;
        this.type = type;
        this.key = key;
        this.DefaultValue = defaultValue;
    }

    public object DefaultValue { get; }

    public override async Task InvokeCallback(object value)
    {
        var task = Callback?.Invoke(new[] { callingType, type, key, value });
        if (task != null) await task;
    }
}