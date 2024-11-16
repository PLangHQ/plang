using LightInject;
using PLang.Utils;

namespace PLang.Modules;

public interface IBuilderFactory
{
    BaseBuilder Create(string builderName);
}

public class BuilderFactory : IBuilderFactory
{
    private readonly ServiceContainer _container;
    private readonly ITypeHelper typeHelper;

    public BuilderFactory(ServiceContainer container, ITypeHelper typeHelper)
    {
        _container = container;
        this.typeHelper = typeHelper;
    }

    public BaseBuilder Create(string builderName)
    {
        // Use reflection to get the type
        var type = typeHelper.GetBuilderType(builderName);
        if (type == null) type = typeof(GenericFunctionBuilder);
        // Use the container to resolve the instance
        return (BaseBuilder)_container.GetInstance(type);
    }
}