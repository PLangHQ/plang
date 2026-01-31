using LightInject;
using PLang.Interfaces;

namespace PLang.Container
{
    public interface IServiceContainerFactory
    {
        ServiceContainer CreateContainer(PLangAppContext context, string path, string goalPath);
    }
}
