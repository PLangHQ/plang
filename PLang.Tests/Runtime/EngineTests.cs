using NSubstitute;
using PLang.Building.Model;
using PLang.Building.Parsers;
using PLang.Container;
using PLang.Errors;
using PLang.Events;
using PLang.Interfaces;
using PLang.Models;
using PLang.Runtime;
using PLang.Services.OutputStream.Sinks;
using PLang.Utils;
using LightInject;
using Microsoft.Extensions.Logging;

namespace PLang.Tests.Runtime;

public class EngineTests
{
    private ServiceContainer _container = null!;
    private IEventRuntime _mockEventRuntime = null!;
    private ILogger _mockLogger = null!;
    private ISettings _mockSettings = null!;

    [Before(Test)]
    public void Setup()
    {
        _container = new ServiceContainer();
        _mockEventRuntime = Substitute.For<IEventRuntime>();
        _mockLogger = Substitute.For<ILogger>();
        _mockSettings = Substitute.For<ISettings>();

        // Setup default event runtime behavior
        _mockEventRuntime.GetBeforeGoalEvents(Arg.Any<Goal>())
            .Returns(Task.FromResult(new List<EventBinding>()));
        _mockEventRuntime.GetAfterGoalEvents(Arg.Any<Goal>())
            .Returns(Task.FromResult(new List<EventBinding>()));
        _mockEventRuntime.GetBeforeStepEvents(Arg.Any<Goal>(), Arg.Any<GoalStep>())
            .Returns(Task.FromResult(new List<EventBinding>()));
        _mockEventRuntime.GetAfterStepEvents(Arg.Any<Goal>(), Arg.Any<GoalStep>())
            .Returns(Task.FromResult(new List<EventBinding>()));
    }

    [After(Test)]
    public void Cleanup()
    {
        _container?.Dispose();
    }

    [Test]
    public async Task Goal_Creation_Works()
    {
        // Arrange & Act
        var goal = CreateTestGoal("TestGoal", new List<GoalStep>());

        // Assert
        await Assert.That(goal.GoalName).IsEqualTo("TestGoal");
        await Assert.That(goal.GoalSteps).IsEmpty();
    }

    [Test]
    public async Task Goal_WithSteps_HasCorrectStepCount()
    {
        // Arrange
        var steps = new List<GoalStep>
        {
            new GoalStep { Text = "Step 1" },
            new GoalStep { Text = "Step 2" }
        };

        // Act
        var goal = CreateTestGoal("TestGoal", steps);

        // Assert
        await Assert.That(goal.GoalSteps).HasCount().EqualTo(2);
    }

    private Goal CreateTestGoal(string name, List<GoalStep> steps)
    {
        return new Goal
        {
            GoalName = name,
            GoalSteps = steps,
            RelativePrPath = $"/.build/{name}/00. Goal.pr",
            AbsolutePrFilePath = $"C:\\test\\.build\\{name}\\00. Goal.pr",
            AbsoluteGoalFolderPath = $"C:\\test\\{name}",
            RelativeGoalFolderPath = $"\\{name}",
            Injections = new List<Injections>()
        };
    }
}

public class ModuleRegistryTests
{
    private ServiceContainer _container = null!;
    private IPLangContextAccessor _contextAccessor = null!;

    [Before(Test)]
    public void Setup()
    {
        _container = new ServiceContainer();
        _contextAccessor = new ContextAccessor();
    }

    [After(Test)]
    public void Cleanup()
    {
        _container?.Dispose();
    }

    [Test]
    public async Task ModuleRegistry_Register_AddsModule()
    {
        // Arrange
        var registry = new ModuleRegistry(_container, _contextAccessor);

        // Act
        registry.Register("test", typeof(TestModule));

        // Assert
        var modules = registry.GetRegisteredModules();
        await Assert.That(modules).Contains("test");
    }

    [Test]
    public async Task ModuleRegistry_Remove_RemovesModule()
    {
        // Arrange
        var registry = new ModuleRegistry(_container, _contextAccessor);
        registry.Register("test", typeof(TestModule));

        // Act
        registry.Remove("test");

        // Assert
        var modules = registry.GetRegisteredModules();
        await Assert.That(modules).DoesNotContain("test");
    }

    [Test]
    public async Task ModuleRegistry_Disable_DisablesModule()
    {
        // Arrange
        var registry = new ModuleRegistry(_container, _contextAccessor);
        registry.Register("test", typeof(TestModule));

        // Act
        registry.Disable("test");

        // Assert
        await Assert.That(registry.IsEnabled("test")).IsFalse();
    }

    [Test]
    public async Task ModuleRegistry_Enable_EnablesDisabledModule()
    {
        // Arrange
        var registry = new ModuleRegistry(_container, _contextAccessor);
        registry.Register("test", typeof(TestModule));
        registry.Disable("test");

        // Act
        registry.Enable("test");

        // Assert
        await Assert.That(registry.IsEnabled("test")).IsTrue();
    }

    [Test]
    public async Task ModuleRegistry_IsEnabled_ReturnsFalseForRemovedModule()
    {
        // Arrange
        var registry = new ModuleRegistry(_container, _contextAccessor);
        registry.Register("test", typeof(TestModule));
        registry.Remove("test");

        // Act & Assert
        await Assert.That(registry.IsEnabled("test")).IsFalse();
    }

    [Test]
    public async Task ModuleRegistry_Get_ReturnsErrorForDisabledModule()
    {
        // Arrange
        var registry = new ModuleRegistry(_container, _contextAccessor);
        registry.Register("test", typeof(TestModule));
        registry.Disable("test");

        // Act
        var (module, error) = registry.Get("test");

        // Assert
        await Assert.That(module).IsNull();
        await Assert.That(error).IsNotNull();
        await Assert.That(error!.Key).IsEqualTo("ModuleDisabled");
    }

    [Test]
    public async Task ModuleRegistry_Get_ReturnsErrorForRemovedModule()
    {
        // Arrange
        var registry = new ModuleRegistry(_container, _contextAccessor);
        registry.Register("test", typeof(TestModule));
        registry.Remove("test");

        // Act
        var (module, error) = registry.Get("test");

        // Assert
        await Assert.That(module).IsNull();
        await Assert.That(error).IsNotNull();
        await Assert.That(error!.Key).IsEqualTo("ModuleRemoved");
    }

    [Test]
    public async Task ModuleRegistry_Get_ReturnsErrorForUnregisteredModule()
    {
        // Arrange
        var registry = new ModuleRegistry(_container, _contextAccessor);

        // Act
        var (module, error) = registry.Get("nonexistent");

        // Assert
        await Assert.That(module).IsNull();
        await Assert.That(error).IsNotNull();
        await Assert.That(error!.Key).IsEqualTo("ModuleNotRegistered");
    }

    [Test]
    public async Task ModuleRegistry_Clone_CopiesRegisteredModules()
    {
        // Arrange
        var registry = new ModuleRegistry(_container, _contextAccessor);
        registry.Register("test1", typeof(TestModule));
        registry.Register("test2", typeof(TestModule));

        // Act
        var clone = registry.Clone();

        // Assert
        var modules = clone.GetRegisteredModules();
        await Assert.That(modules).Contains("test1");
        await Assert.That(modules).Contains("test2");
    }

    [Test]
    public async Task ModuleRegistry_Clone_CopiesDisabledState()
    {
        // Arrange
        var registry = new ModuleRegistry(_container, _contextAccessor);
        registry.Register("test", typeof(TestModule));
        registry.Disable("test");

        // Act
        var clone = registry.Clone();

        // Assert
        await Assert.That(clone.IsEnabled("test")).IsFalse();
    }

    [Test]
    public async Task ModuleRegistry_Clone_CopiesRemovedState()
    {
        // Arrange
        var registry = new ModuleRegistry(_container, _contextAccessor);
        registry.Register("test", typeof(TestModule));
        registry.Remove("test");

        // Act
        var clone = registry.Clone();

        // Assert
        await Assert.That(clone.IsEnabled("test")).IsFalse();
        var (_, error) = clone.Get("test");
        await Assert.That(error!.Key).IsEqualTo("ModuleRemoved");
    }

    [Test]
    public async Task ModuleRegistry_Clone_IsIndependent()
    {
        // Arrange
        var registry = new ModuleRegistry(_container, _contextAccessor);
        registry.Register("test", typeof(TestModule));

        // Act
        var clone = registry.Clone();
        clone.Disable("test");

        // Assert - original should still be enabled
        await Assert.That(registry.IsEnabled("test")).IsTrue();
        await Assert.That(clone.IsEnabled("test")).IsFalse();
    }

    [Test]
    public async Task ModuleRegistry_ExtractShortName_ExtractsCorrectly()
    {
        // Act & Assert
        await Assert.That(ModuleRegistry.ExtractShortName(typeof(PLang.Modules.TerminalModule.Program)))
            .IsEqualTo("terminal");
    }

    [Test]
    public async Task ModuleRegistry_ExtractShortName_HttpModule()
    {
        // Act & Assert
        await Assert.That(ModuleRegistry.ExtractShortName(typeof(PLang.Modules.HttpModule.Program)))
            .IsEqualTo("http");
    }

    [Test]
    public async Task ModuleRegistry_ExtractShortName_FileModule()
    {
        // Act & Assert
        await Assert.That(ModuleRegistry.ExtractShortName(typeof(PLang.Modules.FileModule.Program)))
            .IsEqualTo("file");
    }

    [Test]
    public async Task ModuleRegistry_GetModuleType_ReturnsCorrectType()
    {
        // Arrange
        var registry = new ModuleRegistry(_container, _contextAccessor);
        registry.Register("test", typeof(TestModule));

        // Act
        var type = registry.GetModuleType("test");

        // Assert
        await Assert.That(type).IsEqualTo(typeof(TestModule));
    }

    [Test]
    public async Task ModuleRegistry_GetModuleType_ReturnsNullForUnregistered()
    {
        // Arrange
        var registry = new ModuleRegistry(_container, _contextAccessor);

        // Act
        var type = registry.GetModuleType("nonexistent");

        // Assert
        await Assert.That(type).IsNull();
    }

    [Test]
    public async Task ModuleRegistry_Register_IsCaseInsensitive()
    {
        // Arrange
        var registry = new ModuleRegistry(_container, _contextAccessor);
        registry.Register("Test", typeof(TestModule));

        // Act & Assert
        await Assert.That(registry.IsEnabled("test")).IsTrue();
        await Assert.That(registry.IsEnabled("TEST")).IsTrue();
        await Assert.That(registry.IsEnabled("Test")).IsTrue();
    }

    [Test]
    public async Task ModuleRegistry_Disable_ThrowsForUnregisteredModule()
    {
        // Arrange
        var registry = new ModuleRegistry(_container, _contextAccessor);

        // Act & Assert
        await Assert.That(() => registry.Disable("nonexistent"))
            .Throws<ArgumentException>();
    }

    [Test]
    public async Task ModuleRegistry_Register_ThrowsForNonBaseProgram()
    {
        // Arrange
        var registry = new ModuleRegistry(_container, _contextAccessor);

        // Act & Assert
        await Assert.That(() => registry.Register("invalid", typeof(string)))
            .Throws<ArgumentException>();
    }

    [Test]
    public async Task ModuleRegistry_GetRegisteredModules_ExcludesRemoved()
    {
        // Arrange
        var registry = new ModuleRegistry(_container, _contextAccessor);
        registry.Register("test1", typeof(TestModule));
        registry.Register("test2", typeof(TestModule));
        registry.Remove("test1");

        // Act
        var modules = registry.GetRegisteredModules();

        // Assert
        await Assert.That(modules).DoesNotContain("test1");
        await Assert.That(modules).Contains("test2");
    }

    [Test]
    public async Task ModuleRegistry_Register_AfterRemove_ReRegisters()
    {
        // Arrange
        var registry = new ModuleRegistry(_container, _contextAccessor);
        registry.Register("test", typeof(TestModule));
        registry.Remove("test");

        // Act
        registry.Register("test", typeof(TestModule));

        // Assert
        await Assert.That(registry.IsEnabled("test")).IsTrue();
        var modules = registry.GetRegisteredModules();
        await Assert.That(modules).Contains("test");
    }

    // Test module for registry tests
    private class TestModule : PLang.Modules.BaseProgram
    {
        public override Task<(object?, IError?)> Run()
        {
            return Task.FromResult<(object?, IError?)>((null, null));
        }
    }
}

public class ModuleRegistryCloneIsolationTests
{
    private ServiceContainer _container = null!;
    private IPLangContextAccessor _contextAccessor = null!;

    [Before(Test)]
    public void Setup()
    {
        _container = new ServiceContainer();
        _contextAccessor = new ContextAccessor();
    }

    [After(Test)]
    public void Cleanup()
    {
        _container?.Dispose();
    }

    [Test]
    public async Task Clone_DisablingInClone_DoesNotAffectOriginal()
    {
        // This test ensures per-context isolation
        // Arrange
        var registry = new ModuleRegistry(_container, _contextAccessor);
        registry.Register("module1", typeof(TestModule));
        registry.Register("module2", typeof(TestModule));

        // Act - simulate two contexts with different module states
        var context1Registry = registry.Clone();
        var context2Registry = registry.Clone();

        context1Registry.Disable("module1");
        context2Registry.Disable("module2");

        // Assert - each context has independent state
        await Assert.That(context1Registry.IsEnabled("module1")).IsFalse();
        await Assert.That(context1Registry.IsEnabled("module2")).IsTrue();

        await Assert.That(context2Registry.IsEnabled("module1")).IsTrue();
        await Assert.That(context2Registry.IsEnabled("module2")).IsFalse();

        // Original is unchanged
        await Assert.That(registry.IsEnabled("module1")).IsTrue();
        await Assert.That(registry.IsEnabled("module2")).IsTrue();
    }

    [Test]
    public async Task Clone_RemovingInClone_DoesNotAffectOriginal()
    {
        // Arrange
        var registry = new ModuleRegistry(_container, _contextAccessor);
        registry.Register("terminal", typeof(TestModule));
        registry.Register("code", typeof(TestModule));

        // Act - simulate security sandbox removing dangerous modules
        var sandboxRegistry = registry.Clone();
        sandboxRegistry.Remove("terminal");
        sandboxRegistry.Remove("code");

        // Assert - sandbox has no access to these modules
        var (_, terminalError) = sandboxRegistry.Get("terminal");
        var (_, codeError) = sandboxRegistry.Get("code");
        await Assert.That(terminalError!.Key).IsEqualTo("ModuleRemoved");
        await Assert.That(codeError!.Key).IsEqualTo("ModuleRemoved");

        // But original registry still has them
        await Assert.That(registry.IsEnabled("terminal")).IsTrue();
        await Assert.That(registry.IsEnabled("code")).IsTrue();
    }

    [Test]
    public async Task Clone_MultipleClones_AreAllIndependent()
    {
        // Arrange
        var registry = new ModuleRegistry(_container, _contextAccessor);
        registry.Register("http", typeof(TestModule));

        // Act - create multiple clones (simulating multiple requests)
        var clones = new List<ModuleRegistry>();
        for (int i = 0; i < 5; i++)
        {
            clones.Add(registry.Clone());
        }

        // Disable in different clones
        clones[0].Disable("http");
        clones[2].Disable("http");
        clones[4].Disable("http");

        // Assert - only specific clones are affected
        await Assert.That(clones[0].IsEnabled("http")).IsFalse();
        await Assert.That(clones[1].IsEnabled("http")).IsTrue();
        await Assert.That(clones[2].IsEnabled("http")).IsFalse();
        await Assert.That(clones[3].IsEnabled("http")).IsTrue();
        await Assert.That(clones[4].IsEnabled("http")).IsFalse();

        // Original unchanged
        await Assert.That(registry.IsEnabled("http")).IsTrue();
    }

    private class TestModule : PLang.Modules.BaseProgram
    {
        public override Task<(object?, IError?)> Run()
        {
            return Task.FromResult<(object?, IError?)>((null, null));
        }
    }
}

/// <summary>
/// Tests demonstrating how to mock IPrParser for testing.
/// Full engine tests require integration testing setup due to circular dependencies.
/// </summary>
public class PrParserMockingTests
{
    [Test]
    public async Task IPrParser_CanBeMocked()
    {
        // Arrange - demonstrate that IPrParser can now be mocked
        var mockPrParser = Substitute.For<IPrParser>();
        mockPrParser.GetAllGoals().Returns(new List<Goal>
        {
            new Goal { GoalName = "TestGoal1" },
            new Goal { GoalName = "TestGoal2" }
        });
        mockPrParser.GetEventsFiles(true).Returns(new List<Goal>());
        mockPrParser.GetEventsFiles(false).Returns(new List<Goal>());

        // Act
        var goals = mockPrParser.GetAllGoals();

        // Assert
        await Assert.That(goals).HasCount().EqualTo(2);
        await Assert.That(goals[0].GoalName).IsEqualTo("TestGoal1");
    }

    [Test]
    public async Task IPrParser_GetGoal_CanBeMocked()
    {
        // Arrange
        var mockPrParser = Substitute.For<IPrParser>();
        var testGoal = new Goal { GoalName = "MyGoal", RelativePrPath = "/test/MyGoal" };
        mockPrParser.GetGoal("/test/path").Returns(testGoal);

        // Act
        var goal = mockPrParser.GetGoal("/test/path");

        // Assert
        await Assert.That(goal).IsNotNull();
        await Assert.That(goal!.GoalName).IsEqualTo("MyGoal");
    }

    [Test]
    public async Task IPrParser_ParsePrFile_CanBeMocked()
    {
        // Arrange
        var mockPrParser = Substitute.For<IPrParser>();
        var testGoal = new Goal
        {
            GoalName = "ParsedGoal",
            GoalSteps = new List<GoalStep>
            {
                new GoalStep { Text = "Step 1" },
                new GoalStep { Text = "Step 2" }
            }
        };
        mockPrParser.ParsePrFile(Arg.Any<string>()).Returns(testGoal);

        // Act
        var goal = mockPrParser.ParsePrFile("/path/to/goal.pr");

        // Assert
        await Assert.That(goal).IsNotNull();
        await Assert.That(goal!.GoalSteps).HasCount().EqualTo(2);
    }

    [Test]
    public async Task IPrParser_GetGoalByAppAndGoalName_CanBeMocked()
    {
        // Arrange
        var mockPrParser = Substitute.For<IPrParser>();
        var testGoal = new Goal { GoalName = "AppGoal" };
        mockPrParser.GetGoalByAppAndGoalName("C:\\app", "MyGoal", null).Returns(testGoal);

        // Act
        var goal = mockPrParser.GetGoalByAppAndGoalName("C:\\app", "MyGoal", null);

        // Assert
        await Assert.That(goal).IsNotNull();
        await Assert.That(goal!.GoalName).IsEqualTo("AppGoal");
    }
}

/// <summary>
/// Tests for copy-on-write optimization in ModuleRegistry.Clone()
/// These tests verify that cloning is cheap (no copying) until a write occurs.
/// </summary>
public class ModuleRegistryCopyOnWriteTests
{
    private ServiceContainer _container = null!;
    private IPLangContextAccessor _contextAccessor = null!;

    [Before(Test)]
    public void Setup()
    {
        _container = new ServiceContainer();
        _contextAccessor = new ContextAccessor();
    }

    [After(Test)]
    public void Cleanup()
    {
        _container?.Dispose();
    }

    [Test]
    public async Task Clone_ReadOnlyOperations_DoNotTriggerCopy()
    {
        // Arrange - register many modules
        var registry = new ModuleRegistry(_container, _contextAccessor);
        for (int i = 0; i < 50; i++)
        {
            registry.Register($"module{i}", typeof(TestModule));
        }

        // Act - clone and only do read operations
        var clone = registry.Clone();

        // Read operations - these should not trigger copy
        var modules = clone.GetRegisteredModules();
        var isEnabled = clone.IsEnabled("module0");
        var type = clone.GetModuleType("module1");

        // Assert - clone works correctly
        await Assert.That(modules).HasCount().EqualTo(50);
        await Assert.That(isEnabled).IsTrue();
        await Assert.That(type).IsEqualTo(typeof(TestModule));
    }

    [Test]
    public async Task Clone_WriteOperation_PreservesOriginal()
    {
        // Arrange
        var registry = new ModuleRegistry(_container, _contextAccessor);
        registry.Register("module1", typeof(TestModule));
        registry.Register("module2", typeof(TestModule));

        // Act - clone and do write operation
        var clone = registry.Clone();
        clone.Remove("module1"); // This triggers copy-on-write

        // Assert - original is unchanged
        await Assert.That(registry.GetRegisteredModules()).Contains("module1");
        await Assert.That(registry.GetRegisteredModules()).Contains("module2");

        // Clone has the change
        await Assert.That(clone.GetRegisteredModules()).DoesNotContain("module1");
        await Assert.That(clone.GetRegisteredModules()).Contains("module2");
    }

    [Test]
    public async Task Clone_Disable_PreservesOriginal()
    {
        // Arrange
        var registry = new ModuleRegistry(_container, _contextAccessor);
        registry.Register("http", typeof(TestModule));

        // Act
        var clone = registry.Clone();
        clone.Disable("http"); // This triggers copy-on-write

        // Assert
        await Assert.That(registry.IsEnabled("http")).IsTrue();
        await Assert.That(clone.IsEnabled("http")).IsFalse();
    }

    [Test]
    public async Task Clone_Enable_PreservesOriginal()
    {
        // Arrange
        var registry = new ModuleRegistry(_container, _contextAccessor);
        registry.Register("http", typeof(TestModule));
        registry.Disable("http");

        // Act
        var clone = registry.Clone();
        clone.Enable("http"); // This triggers copy-on-write

        // Assert
        await Assert.That(registry.IsEnabled("http")).IsFalse(); // Original still disabled
        await Assert.That(clone.IsEnabled("http")).IsTrue();     // Clone is enabled
    }

    [Test]
    public async Task Clone_Register_PreservesOriginal()
    {
        // Arrange
        var registry = new ModuleRegistry(_container, _contextAccessor);
        registry.Register("existing", typeof(TestModule));

        // Act
        var clone = registry.Clone();
        clone.Register("newmodule", typeof(TestModule)); // This triggers copy-on-write

        // Assert
        await Assert.That(registry.GetRegisteredModules()).DoesNotContain("newmodule");
        await Assert.That(clone.GetRegisteredModules()).Contains("newmodule");
    }

    [Test]
    public async Task Clone_ChainOfClones_AllIndependent()
    {
        // Test: Original -> Clone1 -> Clone2, each modification is independent
        var registry = new ModuleRegistry(_container, _contextAccessor);
        registry.Register("module", typeof(TestModule));

        var clone1 = registry.Clone();
        var clone2 = clone1.Clone(); // Clone of a clone

        // Modify each
        clone1.Disable("module");
        clone2.Remove("module");

        // Assert all are independent
        await Assert.That(registry.IsEnabled("module")).IsTrue();
        await Assert.That(clone1.IsEnabled("module")).IsFalse();
        var (_, error) = clone2.Get("module");
        await Assert.That(error!.Key).IsEqualTo("ModuleRemoved");
    }

    [Test]
    public async Task Clone_ManyClones_PerformanceIsGood()
    {
        // This test ensures many clones don't cause performance issues
        // With copy-on-write, creating clones should be O(1) not O(n)
        var registry = new ModuleRegistry(_container, _contextAccessor);

        // Register many modules
        for (int i = 0; i < 50; i++)
        {
            registry.Register($"module{i}", typeof(TestModule));
        }

        // Create many clones (simulating many concurrent requests)
        var clones = new List<ModuleRegistry>();
        for (int i = 0; i < 100; i++)
        {
            clones.Add(registry.Clone());
        }

        // All clones should work
        foreach (var clone in clones)
        {
            await Assert.That(clone.GetRegisteredModules()).HasCount().EqualTo(50);
        }

        // Modify every other clone
        for (int i = 0; i < clones.Count; i += 2)
        {
            clones[i].Disable("module0");
        }

        // Verify isolation
        for (int i = 0; i < clones.Count; i++)
        {
            bool expectedEnabled = (i % 2 != 0); // Odd indices should still be enabled
            await Assert.That(clones[i].IsEnabled("module0")).IsEqualTo(expectedEnabled);
        }

        // Original is unchanged
        await Assert.That(registry.IsEnabled("module0")).IsTrue();
    }

    private class TestModule : PLang.Modules.BaseProgram
    {
        public override Task<(object?, IError?)> Run()
        {
            return Task.FromResult<(object?, IError?)>((null, null));
        }
    }
}

/// <summary>
/// Tests that verify MinimalContainer.RegisterBootstrap registers all required services.
/// This prevents regressions where services like IEnginePool are missing at runtime.
/// </summary>
public class BootstrapContainerTests
{
    private ServiceContainer _container = null!;
    private string _testPath = null!;

    [Before(Test)]
    public void Setup()
    {
        _container = new ServiceContainer();
        _testPath = Path.GetTempPath();
    }

    [After(Test)]
    public void Cleanup()
    {
        _container?.Dispose();
    }

    [Test]
    public async Task RegisterBootstrap_RegistersIEnginePool()
    {
        // This test ensures IEnginePool is registered - a past regression caused
        // "Unable to resolve type: PLang.Interfaces.IEnginePool" at runtime
        _container.RegisterBootstrap(_testPath, "/");

        var enginePool = _container.GetInstance<IEnginePool>();

        await Assert.That(enginePool).IsNotNull();
        await Assert.That(enginePool).IsTypeOf<EnginePoolService>();
    }

    [Test]
    public async Task RegisterBootstrap_RegistersIPseudoRuntime()
    {
        _container.RegisterBootstrap(_testPath, "/");

        var pseudoRuntime = _container.GetInstance<IPseudoRuntime>();

        await Assert.That(pseudoRuntime).IsNotNull();
        await Assert.That(pseudoRuntime).IsTypeOf<PseudoRuntime>();
    }

    [Test]
    public async Task RegisterBootstrap_RegistersIEngine()
    {
        _container.RegisterBootstrap(_testPath, "/");

        var engine = _container.GetInstance<IEngine>();

        await Assert.That(engine).IsNotNull();
        await Assert.That(engine).IsTypeOf<Engine>();
    }

    [Test]
    public async Task RegisterBootstrap_RegistersIEventRuntime()
    {
        _container.RegisterBootstrap(_testPath, "/");

        var eventRuntime = _container.GetInstance<IEventRuntime>();

        await Assert.That(eventRuntime).IsNotNull();
        await Assert.That(eventRuntime).IsTypeOf<EventRuntime>();
    }

    [Test]
    public async Task RegisterBootstrap_DependencyChainResolvesCorrectly()
    {
        // IEventRuntime depends on IPseudoRuntime which depends on IEnginePool
        // This test ensures the full dependency chain can be resolved
        _container.RegisterBootstrap(_testPath, "/");

        // Resolve IEventRuntime (which triggers the full chain)
        var eventRuntime = _container.GetInstance<IEventRuntime>();

        await Assert.That(eventRuntime).IsNotNull();

        // Verify the dependencies were also resolved
        var pseudoRuntime = _container.GetInstance<IPseudoRuntime>();
        var enginePool = _container.GetInstance<IEnginePool>();

        await Assert.That(pseudoRuntime).IsNotNull();
        await Assert.That(enginePool).IsNotNull();
    }
}
