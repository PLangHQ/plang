using LightInject;
using Microsoft.Extensions.Logging;
using PLang.Interfaces;
using PLang.Runtime;
using PLang.SafeFileSystem;
using PLang.Services.LlmService;
using PLang.Services.SettingsService;
using PLang.Utils;

namespace PLang.Container;

/// <summary>
/// Bundles commonly used services for module initialization.
/// This reduces multiple DI lookups to a single lookup per step execution.
/// </summary>
public class ModuleServices
{
	public ILogger Logger { get; }
	public PLangAppContext AppContext { get; }
	public IAppCache AppCache { get; }
	public IPLangFileSystem FileSystem { get; }
	public ISettings Settings { get; }
	public IEngine Engine { get; }
	public ITypeHelper TypeHelper { get; }
	public ILlmServiceFactory LlmServiceFactory { get; }
	public MethodHelper MethodHelper { get; }
	public IFileAccessHandler FileAccessHandler { get; }

	public ModuleServices(
		ILogger logger,
		PLangAppContext appContext,
		IAppCache appCache,
		IPLangFileSystem fileSystem,
		ISettings settings,
		IEngine engine,
		ITypeHelper typeHelper,
		ILlmServiceFactory llmServiceFactory,
		MethodHelper methodHelper,
		IFileAccessHandler fileAccessHandler)
	{
		Logger = logger;
		AppContext = appContext;
		AppCache = appCache;
		FileSystem = fileSystem;
		Settings = settings;
		Engine = engine;
		TypeHelper = typeHelper;
		LlmServiceFactory = llmServiceFactory;
		MethodHelper = methodHelper;
		FileAccessHandler = fileAccessHandler;
	}
}
