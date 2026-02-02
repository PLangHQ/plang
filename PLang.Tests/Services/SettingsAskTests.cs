using Microsoft.Extensions.Logging;
using NSubstitute;
using PLang.Building.Model;
using PLang.Building.Parsers;
using PLang.Errors;
using PLang.Exceptions;
using PLang.Interfaces;
using PLang.Models;
using PLang.Runtime;
using PLang.Services.OutputStream.Sinks;
using PLang.Services.SettingsService;
using PLang.Utils;

namespace PLang.Tests.Services;

public class SettingsAskTests
{
	private ISettings _settings;
	private ISettingsRepositoryFactory _settingsRepoFactory;
	private ISettingsRepository _settingsRepo;
	private IEngine _engine;
	private IPLangFileSystem _fileSystem;
	private IPLangContextAccessor _contextAccessor;
	private PLangContext _context;
	private MemoryStack _memoryStack;

	[Before(Test)]
	public void Setup()
	{
		_settingsRepo = Substitute.For<ISettingsRepository>();
		_settingsRepoFactory = Substitute.For<ISettingsRepositoryFactory>();
		_settingsRepoFactory.CreateHandler().Returns(_settingsRepo);

		_engine = Substitute.For<IEngine>();
		_engine.SystemSink.Returns(Substitute.For<IOutputSink>());
		_engine.UserSink.Returns(Substitute.For<IOutputSink>());
		_engine.CloneDefaultModuleRegistry().Returns(Substitute.For<ModuleRegistry>(Substitute.For<LightInject.IServiceContainer>(), Substitute.For<IPLangContextAccessor>()));
		_engine.Environment.Returns("Development"); // Settings.GetKey() prepends environment to key

		_fileSystem = Substitute.For<IPLangFileSystem>();
		_fileSystem.RootDirectory.Returns("C:\\test");

		// Create real MemoryStack with mocked dependencies
		var pseudoRuntime = Substitute.For<IPseudoRuntime>();
		var settingsForMemory = Substitute.For<ISettings>();
		var logger = Substitute.For<ILogger>();
		var variableHelper = new VariableHelper(settingsForMemory, logger);
		_contextAccessor = Substitute.For<IPLangContextAccessor>();

		_memoryStack = new MemoryStack(pseudoRuntime, _engine, settingsForMemory, variableHelper, _contextAccessor);
		_context = new PLangContext(_memoryStack, _engine, ExecutionMode.Console);
		_contextAccessor.Current.Returns(_context);

		_settings = new Settings(_engine, _settingsRepoFactory, _fileSystem);
	}

	[Test]
	public async Task Settings_Get_ThrowsMissingSettingsException_WhenKeyNotFound()
	{
		// Arrange
		_settingsRepo.Get(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>()).Returns((Setting?)null);

		// Act & Assert
		var exception = await Assert.ThrowsAsync<MissingSettingsException>(async () =>
		{
			_settings.Get<string>(typeof(Settings), "TestApiKey", "", "Please provide your API key");
		});

		await Assert.That(exception).IsNotNull();
		// Key is prefixed with environment (e.g., "Development.TestApiKey")
		await Assert.That(exception!.Key).IsEqualTo("Development.TestApiKey");
		await Assert.That(exception.Message).IsEqualTo("Please provide your API key");
	}

	[Test]
	public async Task MissingSettingsException_InvokeCallback_SavesSetting()
	{
		// Arrange
		Setting? savedSetting = null;
		_settingsRepo.Get(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>()).Returns((Setting?)null);
		_settingsRepo.When(x => x.Set(Arg.Any<Setting>()))
			.Do(x => savedSetting = x.Arg<Setting>());

		MissingSettingsException? capturedException = null;
		try
		{
			_settings.Get<string>(typeof(Settings), "TestApiKey", "", "Please provide your API key");
		}
		catch (MissingSettingsException mse)
		{
			capturedException = mse;
		}

		// Act - Invoke the callback with a user-provided value
		var error = await capturedException!.InvokeCallback("my-secret-api-key");

		// Assert
		await Assert.That(error).IsNull();
		await Assert.That(savedSetting).IsNotNull();
		// Key is prefixed with environment (e.g., "Development.TestApiKey")
		await Assert.That(savedSetting!.Key).IsEqualTo("Development.TestApiKey");
		await Assert.That(savedSetting.Value).Contains("my-secret-api-key");
	}

	[Test]
	public async Task Settings_Get_ReturnsValue_WhenKeyExists()
	{
		// Arrange - key is prefixed with environment
		var setting = new Setting("app-id", "PLang.Services.SettingsService.Settings", "System.String", "Development.TestApiKey", "\"existing-api-key\"");
		_settingsRepo.Get(Arg.Any<string>(), Arg.Any<string>(), "Development.TestApiKey").Returns(setting);

		// Act
		var result = _settings.Get<string>(typeof(Settings), "TestApiKey", "", "Please provide your API key");

		// Assert
		await Assert.That(result).IsEqualTo("existing-api-key");
	}

	[Test]
	public async Task MissingSettingsException_HasCorrectDefaultValue()
	{
		// Arrange
		_settingsRepo.Get(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>()).Returns((Setting?)null);

		MissingSettingsException? capturedException = null;
		try
		{
			_settings.Get<string>(typeof(Settings), "TestApiKey", "default-value", "Please provide your API key");
		}
		catch (MissingSettingsException mse)
		{
			capturedException = mse;
		}

		// Assert
		await Assert.That(capturedException).IsNotNull();
		await Assert.That(capturedException!.DefaultValue).IsEqualTo("default-value");
	}
}

public class AskUserFlowTests
{
	private IEngine _engine;
	private IPLangContextAccessor _contextAccessor;
	private PLangContext _context;
	private MemoryStack _memoryStack;
	private IPrParser _prParser;

	[Before(Test)]
	public void Setup()
	{
		_engine = Substitute.For<IEngine>();
		_engine.SystemSink.Returns(Substitute.For<IOutputSink>());
		_engine.UserSink.Returns(Substitute.For<IOutputSink>());
		_engine.CloneDefaultModuleRegistry().Returns(Substitute.For<ModuleRegistry>(Substitute.For<LightInject.IServiceContainer>(), Substitute.For<IPLangContextAccessor>()));

		_prParser = Substitute.For<IPrParser>();
		_engine.PrParser.Returns(_prParser);

		// Create real MemoryStack
		var pseudoRuntime = Substitute.For<IPseudoRuntime>();
		var settings = Substitute.For<ISettings>();
		var logger = Substitute.For<ILogger>();
		var variableHelper = new VariableHelper(settings, logger);
		_contextAccessor = Substitute.For<IPLangContextAccessor>();

		_memoryStack = new MemoryStack(pseudoRuntime, _engine, settings, variableHelper, _contextAccessor);
		_context = new PLangContext(_memoryStack, _engine, ExecutionMode.Console);
		_contextAccessor.Current.Returns(_context);
	}

	[Test]
	public async Task AskUser_GetAnswer_ReturnsError_WhenAskSystemGoalNotFound()
	{
		// Arrange
		_prParser.GetEvent("AskSystem").Returns((Goal?)null);
		_prParser.GetSystemEvent("AskSystem").Returns((Goal?)null);

		// Act
		var (answer, error) = await PLang.Events.Types.AskUser.GetAnswer(_engine, _context, "What is your name?");

		// Assert
		await Assert.That(error).IsNotNull();
		await Assert.That(error!.Message).Contains("Ask system goal could not be found");
		await Assert.That(error.FixSuggestion).Contains("AskSystem.goal");
	}

	[Test]
	public async Task AskUser_GetAnswer_SetsQuestionInMemoryStack()
	{
		// Arrange
		var askSystemGoal = new Goal { GoalName = "AskSystem", GoalSteps = new List<GoalStep>() };
		_prParser.GetEvent("AskSystem").Returns(askSystemGoal);

		// Mock engine.RunGoal to return a Return error with a value
		var returnError = new Return(new List<ObjectValue> { new ObjectValue("answer", "test-answer") });
		_engine.RunGoal(Arg.Any<Goal>(), Arg.Any<PLangContext>(), Arg.Any<uint>())
			.Returns(Task.FromResult<(object?, IError?)>((null, returnError)));

		// Act
		var (answer, error) = await PLang.Events.Types.AskUser.GetAnswer(_engine, _context, "What is your name?");

		// Assert
		var questionInMemory = _memoryStack.Get("__plang_question");
		await Assert.That(questionInMemory).IsEqualTo("What is your name?");
	}

	[Test]
	public async Task AskUser_GetAnswer_ReturnsAnswerFromGoal()
	{
		// Arrange
		var askSystemGoal = new Goal { GoalName = "AskSystem", GoalSteps = new List<GoalStep>() };
		_prParser.GetEvent("AskSystem").Returns(askSystemGoal);

		// Mock engine.RunGoal to return a Return with the answer
		var returnError = new Return(new List<ObjectValue> { new ObjectValue("__plang_answer", "user-input-value") });
		_engine.RunGoal(Arg.Any<Goal>(), Arg.Any<PLangContext>(), Arg.Any<uint>())
			.Returns(Task.FromResult<(object?, IError?)>((null, returnError)));

		// Act
		var (answer, error) = await PLang.Events.Types.AskUser.GetAnswer(_engine, _context, "What is your API key?");

		// Assert
		await Assert.That(answer).IsEqualTo("user-input-value");
	}
}
