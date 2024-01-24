using LightInject;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using NSubstitute;
using PLang.Building.Events;
using PLang.Building.Model;
using PLang.Building.Parsers;
using PLang.Interfaces;
using PLang.Runtime;
using PLang.Services.CachingService;
using PLang.Services.OutputStream;
using PLang.Services.SettingsService;
using PLang.Services.SigningService;
using PLang.Utils;
using PLangTests.Mocks;
using System.Data;
using System.Runtime.CompilerServices;
using static PLang.Modules.BaseBuilder;

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
		protected ISettingsRepository settingsRepository;
		protected ISettings settings;
		protected IEventRuntime eventRuntime;
		protected ITypeHelper typeHelper;
		protected IErrorHelper errorHelper;
		protected PrParser prParser;
		protected PLangAppContext context;
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
		protected IAppCache appCache;
		protected IPLangIdentityService identityService;
		protected IPLangSigningService signingService;
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
			this.settingsRepository = new SqliteSettingsRepository(fileSystem, context);
			container.RegisterInstance<ISettingsRepository>(settingsRepository);

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

			appCache = new InMemoryCaching();
			container.RegisterInstance<IAppCache>(appCache, "PLang.Services.CachingService.InMemoryCaching");

			logger = Substitute.For<MockLogger>();
			//logger = new PLang.Utils.Logger<BasePLangTest>();
			container.RegisterInstance<ILogger>(logger);

			identityService = Substitute.For<IPLangIdentityService>();
			container.RegisterInstance(identityService);
			signingService = Substitute.For<IPLangSigningService>();
			container.RegisterInstance(signingService);

			aiService = Substitute.For<ILlmService>();
			container.RegisterInstance(aiService);

			outputStream = Substitute.For<IOutputStream>();
			container.RegisterInstance(outputStream);



			encryption = Substitute.For<IEncryption>();
			container.RegisterInstance(encryption);

			engine = Substitute.For<IEngine>();
			container.RegisterInstance(engine);

			settings = Substitute.For<ISettings>();

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

			container.RegisterInstance(prParser);
			return container;
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
			
			string filePath = Path.Combine(dir, caller + ".json");

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
			llmService.Query(Arg.Any<LlmQuestion>(), type).Returns(p => {
				return JsonConvert.DeserializeObject(testResponse, type);
			});
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
			string filePath = Path.Combine(dir, caller + ".json");
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
				return Path.Combine(testPath, "Modules", moduleFolder, "responses");
			}
			else
			{
				string derivedClassDirectory = Path.GetDirectoryName(derivedClassPath);
				
				string responsesDir = Path.GetFullPath(Path.Combine(derivedClassDirectory, $"../../../Modules/{moduleFolder}/responses"));
				return responsesDir;
			}
		}

	}
}
