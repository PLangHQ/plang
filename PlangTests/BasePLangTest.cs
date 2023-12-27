using LightInject;
using Microsoft.Extensions.Logging;
using NSubstitute;
using PLang.Building.Events;
using PLang.Building.Parsers;
using PLang.Interfaces;
using PLang.Runtime;
using PLang.Services.CachingService;
using PLang.Services.SettingsService;
using PLang.Utils;
using PLangTests.Mocks;
using System.Data;

namespace PLangTests
{
	public class BasePLangTest
	{
		protected ServiceContainer container;

		protected MockLogger logger;
		protected PLangMockFileSystem fileSystem;
		protected ILlmService aiService;
		protected IPseudoRuntime pseudoRuntime;
		protected IEngine engine;
		protected ISettings settings;
		protected IEventRuntime eventRuntime;
		protected ITypeHelper typeHelper;
		protected IErrorHelper errorHelper;
		protected PrParser prParser;
		protected PLangAppContext context;
		protected HttpHelper httpHelper;
		protected HttpClient httpClient;
		protected CacheHelper cacheHelper;
		protected IServiceContainerFactory containerFactory;
		protected MemoryStack memoryStack;
		protected VariableHelper variableHelper;
		protected IDbConnection db;
		protected IArchiver archiver;
		protected IEventSourceRepository eventSourceRepository;
		protected IEncryption encryption;
		protected IOutputStream outputStream;
		protected Signature signature;

		protected void Initialize()
		{

			container = CreateServiceContainer();

		}

		protected ServiceContainer CreateServiceContainer()
		{
			AppContext.SetSwitch(ReservedKeywords.Test, true);
			container = new ServiceContainer();
			context = new PLangAppContext();
			fileSystem = new PLangMockFileSystem();
			fileSystem.AddFile(Path.Join(Environment.CurrentDirectory, ".build", "info.txt"), Guid.NewGuid().ToString());
			container.RegisterInstance<IPLangFileSystem>(fileSystem);
			container.RegisterInstance<IServiceContainer>(container);
			
			container.RegisterInstance<ISettingsRepository>(new SqliteSettingsRepository(fileSystem, context));

			containerFactory = Substitute.For<IServiceContainerFactory>();
			containerFactory.CreateContainer(Arg.Any<PLangAppContext>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<IOutputStream>()).Returns(p =>
			{
				var container = CreateServiceContainer();
				container.GetInstance<IEngine>().GetMemoryStack().Returns(a =>
				{
					return new MemoryStack(pseudoRuntime, engine, context);
				});
				return container;
			});
			container.RegisterInstance<IServiceContainerFactory>(containerFactory);

			context = new PLangAppContext();
			container.RegisterInstance(context);

			context.AddOrReplace(ReservedKeywords.Inject_Caching, typeof(InMemoryCaching).FullName);

			container.RegisterInstance<IAppCache>(new InMemoryCaching(context), "PLang.Services.CachingService.InMemoryCaching");

			logger = Substitute.For<MockLogger>();
			//logger = new PLang.Utils.Logger<BasePLangTest>();
			container.RegisterInstance<ILogger>(logger);

			aiService = Substitute.For<ILlmService>();
			container.RegisterInstance(aiService);

			outputStream = Substitute.For<IOutputStream>();
			container.RegisterInstance(outputStream);

			

			encryption = Substitute.For<IEncryption>();
			container.RegisterInstance(encryption);

			engine = Substitute.For<IEngine>();
			container.RegisterInstance(engine);

			settings = Substitute.For<ISettings>();
			settings.GoalsPath.Returns(Environment.CurrentDirectory);
			settings.BuildPath.Returns(Path.Join(Environment.CurrentDirectory, ".build"));

			container.RegisterInstance<ISettings>(settings);
			pseudoRuntime = Substitute.For<IPseudoRuntime>();
			container.RegisterInstance(pseudoRuntime);

			eventRuntime = Substitute.For<IEventRuntime>();
			container.RegisterInstance(eventRuntime);

			db = Substitute.For<IDbConnection>();
			//container.RegisterInstance(db);

			eventSourceRepository = Substitute.For<IEventSourceRepository>();
			container.RegisterInstance(eventSourceRepository);

			container.Register<EventBuilder>();

			container.Register<IGoalParser, GoalParser>();

			typeHelper = Substitute.For<ITypeHelper>();
			container.RegisterInstance(typeHelper);

			errorHelper = Substitute.For<IErrorHelper>();
			container.RegisterInstance(errorHelper);

			memoryStack = new MemoryStack(pseudoRuntime, engine, context);
			container.RegisterInstance(memoryStack);
			
			archiver = Substitute.For<IArchiver>();
			container.RegisterInstance(archiver);

			prParser = new PrParser(fileSystem, settings);
			container.RegisterInstance(prParser);

			cacheHelper = new CacheHelper(fileSystem, settings);
			container.RegisterInstance(cacheHelper);

			variableHelper = new VariableHelper(context, memoryStack, settings);
			container.RegisterInstance(variableHelper);

			signature = new Signature(settings, context);
			container.RegisterInstance(signature);

			httpHelper = new HttpHelper(settings, context, aiService, signature);
			container.RegisterInstance(prParser);
			return container;
		}
	}
}
