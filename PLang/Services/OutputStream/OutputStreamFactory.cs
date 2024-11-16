using LightInject;
using PLang.Interfaces;
using PLang.Utils;

namespace PLang.Services.OutputStream;

public class OutputStreamFactory : BaseFactory, IOutputStreamFactory
{
    private readonly PLangAppContext appContext;
    private readonly string defaultType;
    private string currentType;

    public OutputStreamFactory(IServiceContainer container, string defaultType) : base(container)
    {
        appContext = container.GetInstance<PLangAppContext>();
        this.defaultType = defaultType;
        currentType = defaultType;
    }

    public IOutputStreamFactory SetContext(string? name)
    {
        if (string.IsNullOrEmpty(name))
        {
            appContext.Remove(ReservedKeywords.Inject_OutputStream);
            currentType = defaultType;

            return this;
        }

        currentType = name;
        appContext.AddOrReplace(ReservedKeywords.Inject_OutputStream, name);
        return this;
    }

    public IOutputStream CreateHandler(string[]? acceptTypes = null)
    {
        if (acceptTypes == null) return container.GetInstance<IOutputStream>("text/plain");

        foreach (var acceptType in acceptTypes)
        {
            var formatter = container.GetInstance<IOutputStream>(acceptType);
            if (formatter != null) return formatter;
        }

        return container.GetInstance<IOutputStream>("text/plain");
    }
}