using LightInject;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using NSubstitute;
using PLang.Building.Model;
using PLang.Building.Parsers;
using PLang.Container;
using PLang.Errors.Handlers;
using PLang.Events;
using PLang.Exceptions.AskUser;
using PLang.Interfaces;
using PLang.Models;
using PLang.Modules;
using PLang.Modules.DbModule;
using PLang.Runtime;
using PLang.SafeFileSystem;
using PLang.Services.AppsRepository;
using PLang.Services.CachingService;
using PLang.Services.DbService;
using PLang.Services.LlmService;
using PLang.Services.OpenAi;
using PLang.Services.OutputStream;
using PLang.Services.SettingsService;
using PLang.Services.SigningService;
using PLang.Utils;
using PLangTests.Mocks;
using System.Data;
using System.Reflection;
using System.Runtime.CompilerServices;
using static PLang.Modules.BaseBuilder;

namespace PLangTests
{
	public class BasePLangTest
	{
		protected IServiceContainer container;

		protected MockLogger logger;
		protected PLangMockFileSystem fileSystem;
		protected ILlmService llmService;
		protected IDbServiceFactory dbFactory;
		protected ILlmServiceFactory llmServiceFactory;
		protected IPseudoRuntime pseudoRuntime;
		protected IEngine engine;
		protected EnginePool enginePool;
		protected ISettingsRepository settingsRepository;
		protected ISettings settings;
		protected IEventRuntime eventRuntime;
		protected ITypeHelper typeHelper;
		protected PrParser prParser;
		protected PLangAppContext context;
		protected HttpClient httpClient;
		protected LlmCaching llmCaching;
		protected IServiceContainerFactory containerFactory;
		protected MemoryStack memoryStack;
		protected VariableHelper variableHelper;
		protected IDbConnection db;
		protected IArchiver archiver;
		protected IEventSourceRepository eventSourceRepository;
		protected IEncryption encryption;
		protected IEncryptionFactory encryptionFactory;
		protected IOutputStream outputStream;
		protected IOutputStream outputSystemStream;
		protected IOutputStreamFactory outputStreamFactory;
		protected IOutputSystemStreamFactory outputSystemStreamFactory;
		protected ProgramFactory programFactory;
		protected IAppCache appCache;
		protected IPLangIdentityService identityService;
		protected IPLangSigningService signingService;
		protected IPLangAppsRepository appsRepository;
		protected IHttpClientFactory httpClientFactory;
		protected IAskUserHandler askUserHandler;
		protected IErrorHandler errorHandler;
		protected IErrorHandlerFactory errorHandlerFactory;
		protected IErrorSystemHandlerFactory errorSystemHandlerFactory;
		protected ISettingsRepositoryFactory settingsRepositoryFactory;
		protected IFileAccessHandler fileAccessHandler;
		protected DependancyHelper dependancyHelper;
		protected IGoalParser goalParser;


		protected PLang.Modules.SerializerModule.Program serializer;
		protected PLang.Modules.CryptographicModule.Program crypto;
		protected PLang.Modules.IdentityModule.Program identity;

		protected GoalStep step;
		protected void Initialize()
		{

			container = CreateServiceContainer();

			//serializer = container.GetInstance<PLang.Modules.SerializerModule.Program>();
			//crypto = container.GetInstance<PLang.Modules.CryptographicModule.Program>();


			step = new PLang.Building.Model.GoalStep();
			
		}
		public void LoadStep(string text)
		{
			step.Text = text;
			step.ModuleType = this.GetType().FullName;
		}
		protected void LoadOpenAI()
		{
			settings.Get(typeof(OpenAiService), "Global_AIServiceKey", Arg.Any<string>(), Arg.Any<string>()).Returns(Environment.GetEnvironmentVariable("OpenAIKey"));

			var llmService = new OpenAiService(settings, logger, llmCaching, context);
			llmServiceFactory.CreateHandler().Returns(llmService);
		}
		protected IServiceContainer CreateServiceContainer()
		{
			AppContext.SetSwitch(ReservedKeywords.Test, true);
			container = new ServiceContainer();
			context = new PLangAppContext();
			fileSystem = new PLangMockFileSystem();
			fileSystem.AddFile(System.IO.Path.Join(Environment.CurrentDirectory, ".build", "info.txt"), Guid.NewGuid().ToString());

			dbFactory = Substitute.For<IDbServiceFactory>();
			container.RegisterInstance<IPLangFileSystem>(fileSystem);
			container.RegisterInstance<IServiceContainer>(container); 
			container.RegisterSingleton<IPLangFileSystemFactory>(factory =>
			{
				return new PlangFileSystemFactory(container);
			});
			this.settingsRepository = new SqliteSettingsRepository(container.GetInstance<IPLangFileSystemFactory>(), context, logger);
			container.RegisterInstance<ISettingsRepository>(settingsRepository);
			fileAccessHandler = Substitute.For<IFileAccessHandler>();
			settingsRepositoryFactory = Substitute.For<ISettingsRepositoryFactory>();
			settingsRepositoryFactory.CreateHandler().Returns(settingsRepository);
			container.RegisterInstance<ISettingsRepositoryFactory>(settingsRepositoryFactory);

			containerFactory = Substitute.For<IServiceContainerFactory>();
			containerFactory.CreateContainer(Arg.Any<PLangAppContext>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<IOutputStreamFactory>(), 
				Arg.Any<IOutputSystemStreamFactory>(), Arg.Any<IErrorHandlerFactory>(), Arg.Any<IErrorSystemHandlerFactory>()).Returns(p =>
			{
				var container = CreateServiceContainer();

				IEngine engine = container.GetInstance<IEngine>();
				engine.GetMemoryStack().Returns(a =>
				{
					return new MemoryStack(pseudoRuntime, engine, settings, context);
				});
				return container;
			});
			container.RegisterInstance<IServiceContainerFactory>(containerFactory);

			context = new PLangAppContext();
			container.RegisterInstance(context);

			context.AddOrReplace(ReservedKeywords.Inject_Caching, typeof(InMemoryCaching).FullName);

			appCache = new InMemoryCaching();
			container.RegisterInstance<IAppCache>(appCache, "PLang.Services.CachingService.InMemoryCaching");

			logger = Substitute.For<MockLogger>();
			//logger = new PLang.Utils.Logger<BasePLangTest>();
			container.RegisterInstance<ILogger>(logger);

			identityService = Substitute.For<IPLangIdentityService>();
			container.RegisterInstance(identityService);
			signingService = Substitute.For<IPLangSigningService>();
			container.RegisterInstance(signingService);

			llmService = Substitute.For<ILlmService>();
			container.RegisterInstance(llmService);
			llmServiceFactory = Substitute.For<ILlmServiceFactory>();
			llmServiceFactory.CreateHandler().Returns(llmService);
			container.RegisterInstance(llmServiceFactory);

			outputStream = Substitute.For<IOutputStream>();
			container.RegisterInstance(outputStream);
			outputStreamFactory = Substitute.For<IOutputStreamFactory>();
			outputStreamFactory.CreateHandler().Returns(outputStream);
			container.RegisterInstance(outputStreamFactory);

			outputSystemStream = Substitute.For<IOutputStream>();
			container.RegisterInstance(outputSystemStream);
			outputSystemStreamFactory = Substitute.For<IOutputSystemStreamFactory>();
			outputSystemStreamFactory.CreateHandler().Returns(outputStream);
			container.RegisterInstance(outputStreamFactory);


			httpClientFactory = Substitute.For<IHttpClientFactory>();
			container.RegisterInstance(httpClientFactory);

			encryption = Substitute.For<IEncryption>();
			container.RegisterInstance(encryption);
			encryptionFactory = Substitute.For<IEncryptionFactory>();
			encryptionFactory.CreateHandler().Returns(encryption);

			container.RegisterInstance(encryptionFactory);

			appsRepository = Substitute.For<IPLangAppsRepository>();
			container.RegisterInstance(appsRepository);

			engine = Substitute.For<IEngine>();
			container.RegisterInstance(engine);

			settings = Substitute.For<ISettings>();

			container.RegisterInstance<ISettings>(settings);
			pseudoRuntime = Substitute.For<IPseudoRuntime>();
			container.RegisterInstance(pseudoRuntime);

			eventRuntime = Substitute.For<IEventRuntime>();
			container.RegisterInstance(eventRuntime);

			errorHandler = Substitute.For<IErrorHandler>();
			container.RegisterInstance(errorHandler);
			errorHandlerFactory = Substitute.For<IErrorHandlerFactory>();
			container.RegisterInstance(errorHandlerFactory);

			errorSystemHandlerFactory = Substitute.For<IErrorSystemHandlerFactory>();
			container.RegisterInstance(errorSystemHandlerFactory);
			db = Substitute.For<IDbConnection>();
			//container.RegisterInstance(db);

			eventSourceRepository = Substitute.For<IEventSourceRepository>();
			container.RegisterInstance(eventSourceRepository);

			container.Register<EventBuilder>();

			container.Register<IGoalParser, GoalParser>();
			goalParser = container.GetInstance<IGoalParser>();

			dependancyHelper = new DependancyHelper(fileSystem, logger, prParser);

			typeHelper = new TypeHelper(fileSystem, dependancyHelper);
			container.RegisterInstance(typeHelper);

			memoryStack = new MemoryStack(pseudoRuntime, engine, settings, context);
			container.RegisterInstance(memoryStack);

			archiver = Substitute.For<IArchiver>();
			container.RegisterInstance(archiver);

			prParser = new PrParser(fileSystem, logger);
			container.RegisterInstance(prParser);

			llmCaching = new LlmCaching(fileSystem, settings);
			container.RegisterInstance(llmCaching);

			variableHelper = new VariableHelper(memoryStack, settings, logger);
			container.RegisterInstance(variableHelper);

			container.RegisterInstance(prParser);

			RegisterModules(container);

			return container;
		}


		private static void RegisterModules(IServiceContainer container)
		{

			var currentAssembly = Assembly.GetAssembly(typeof(PLang.Modules.FileModule.Program));

			// Scan the current assembly for types that inherit from BaseBuilder
			var modulesFromCurrentAssembly = currentAssembly.GetTypes()
																.Where(t => !t.IsAbstract && !t.IsInterface &&
																(typeof(BaseBuilder).IsAssignableFrom(t) || typeof(BaseProgram).IsAssignableFrom(t)))
																.ToList();

			// Register these types with the DI container
			foreach (var type in modulesFromCurrentAssembly)
			{
				container.Register(type, type, serviceName: type.FullName);  // or register with a specific interface if needed
			}
			container.Register<BaseBuilder, BaseBuilder>();

			var fileSystem = container.GetInstance<IPLangFileSystem>();
			if (fileSystem.Directory.Exists(".modules"))
			{
				var dependancyHelper = container.GetInstance<DependancyHelper>();
				var modules = dependancyHelper.LoadModules(typeof(BaseProgram), fileSystem.GoalsPath);
				foreach (var module in modules)
				{
					container.Register(module);
				}

			}

		}

		public record TestResponse(string stepText, string response, DateTimeOffset? created = null);

		public void Store(string stepText, string? response, [CallerMemberName] string caller = "")
		{
			if (string.IsNullOrWhiteSpace(response)) return;

			if (string.IsNullOrWhiteSpace(stepText)) throw new Exception("stepText cannot be empty");
			if (string.IsNullOrWhiteSpace(caller)) throw new Exception("caller cannot be empty");

			var dir = GetSourceResponseDir();
			if (!Directory.Exists(dir))
			{
				Directory.CreateDirectory(dir);
			}

			string filePath = System.IO.Path.Join(dir, caller + ".json");

			List<TestResponse> responses = new List<TestResponse>();
			if (File.Exists(filePath))
			{
				var jsonFile = File.ReadAllText(filePath);
				responses = JsonConvert.DeserializeObject<List<TestResponse>>(jsonFile) ?? new List<TestResponse>();
				var idx = responses.FindIndex(p => p.stepText == stepText);
				if (idx != -1)
				{
					return;
				}
			}
			responses.Add(new TestResponse(stepText, response, DateTimeOffset.Now));
			File.WriteAllText(filePath, JsonConvert.SerializeObject(responses, Formatting.Indented));
		}
		public ILlmService? GetLlmService(string stepText, string caller = "", Type? type = null)
		{
			var testResponse = GetLlmTestResponse(stepText, caller);
			if (testResponse == null) return null;

			if (type == null) type = typeof(GenericFunction);

			var llmService = Substitute.For<ILlmService>();
			llmService.Query(Arg.Any<LlmRequest>(), type).Returns(p =>
			{
				return JsonConvert.DeserializeObject(testResponse, type);
			});
			llmServiceFactory.CreateHandler().Returns(llmService);

			return llmService;

		}
		public string? GetLlmTestResponse(string stepText, [CallerMemberName] string caller = "")
		{
			if (string.IsNullOrWhiteSpace(caller)) throw new Exception("caller cannot be empty");

			var dir = GetSourceResponseDir();
			if (!Directory.Exists(dir))
			{
				return null;
			}
			string filePath = System.IO.Path.Join(dir, caller + ".json");
			if (!File.Exists(filePath)) return null;

			var jsonFile = File.ReadAllText(filePath);
			var responses = JsonConvert.DeserializeObject<List<TestResponse>>(jsonFile) ?? new List<TestResponse>();
			var testReponse = responses.FirstOrDefault(p => p.stepText == stepText);
			if (testReponse == null) return null;
			return testReponse.response;
		}

		public string GetSourceResponseDir()
		{
			string derivedClassPath = this.GetType().Assembly.Location;
			string moduleFolder = this.GetType().Namespace.Replace("PLang.Modules.", "").Replace(".Tests", "");
			if (derivedClassPath.ToLower().Contains("ncrunch"))
			{
				string testPath = Environment.GetEnvironmentVariable("PlangTestPath");
				if (string.IsNullOrEmpty(testPath)) throw new Exception("You must set the PlangTestPath environment variable. I should point to PlangTests folder. The PlangTests folder contains Modules folder");
				return System.IO.Path.Join(testPath, "Modules", moduleFolder, "responses");
			}
			else
			{
				string derivedClassDirectory = System.IO.Path.GetDirectoryName(derivedClassPath);

				string responsesDir = System.IO.Path.GetFullPath(System.IO.Path.Join(derivedClassDirectory, $"../../../Modules/{moduleFolder}/responses"));
				return responsesDir;
			}
		}

	}
}
