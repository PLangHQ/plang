using NSubstitute;
using PLang.Building.Model;
using PLang.Container;
using PLang.Errors;
using PLang.Interfaces;
using PLang.Models;
using PLang.Runtime;
using PLang.Utils;
using LightInject;

namespace PLang.Tests.Runtime;

/// <summary>
/// Unit tests for EnginePoolService behavior.
/// Note: Tests run sequentially to avoid static pool interference.
/// </summary>
[NotInParallel]
public class EnginePoolServiceBasicTests
{
    [Before(Test)]
    public void Setup()
    {
        EnginePoolService.DisposeAll();
    }

    [After(Test)]
    public void Cleanup()
    {
        EnginePoolService.DisposeAll();
    }

    [Test]
    public async Task Constructor_InitializesWithEmptyPool()
    {
        // Arrange
        var mockFileSystem = Substitute.For<IPLangFileSystem>();
        mockFileSystem.RootDirectory.Returns("C:\\test");

        // Act
        var pool = new EnginePoolService(mockFileSystem);

        // Assert
        await Assert.That(pool.AvailableCount).IsEqualTo(0);
        await Assert.That(pool.TotalCreated).IsEqualTo(0);
    }

    [Test]
    public async Task Return_WithNullEngine_DoesNotThrow()
    {
        // Arrange
        var mockFileSystem = Substitute.For<IPLangFileSystem>();
        mockFileSystem.RootDirectory.Returns("C:\\test");
        var pool = new EnginePoolService(mockFileSystem);

        var countBefore = pool.AvailableCount;

        // Act & Assert - should not throw
        pool.Return(null!);
        await Assert.That(pool.AvailableCount).IsEqualTo(countBefore);
    }

    [Test]
    public async Task DisposeAll_ClearsStaticState()
    {
        // Arrange
        var mockFileSystem = Substitute.For<IPLangFileSystem>();
        mockFileSystem.RootDirectory.Returns("C:\\test");

        // Add an engine to pool
        var mockEngine = Substitute.For<IEngine>();
        mockEngine.Container.Returns(Substitute.For<IServiceContainer>());
        var pool1 = new EnginePoolService(mockFileSystem);
        pool1.Return(mockEngine);

        // Act
        EnginePoolService.DisposeAll();

        // Assert - pool should be empty
        var pool2 = new EnginePoolService(mockFileSystem);
        await Assert.That(pool2.AvailableCount).IsEqualTo(0);
    }
}

/// <summary>
/// Tests that verify the engine pool return logic handles engine state correctly.
/// </summary>
[NotInParallel]
public class EnginePoolReturnBehaviorTests
{
    [Before(Test)]
    public void Setup()
    {
        EnginePoolService.DisposeAll();
    }

    [After(Test)]
    public void Cleanup()
    {
        EnginePoolService.DisposeAll();
    }

    [Test]
    public async Task Return_ValidEngine_CallsReset()
    {
        // Arrange
        var mockFileSystem = Substitute.For<IPLangFileSystem>();
        mockFileSystem.RootDirectory.Returns("C:\\test");
        var pool = new EnginePoolService(mockFileSystem);

        var mockEngine = Substitute.For<IEngine>();
        mockEngine.Container.Returns(Substitute.For<IServiceContainer>());

        // Act
        pool.Return(mockEngine);

        // Assert
        mockEngine.Received(1).Reset();
    }

    [Test]
    public async Task Return_ValidEngine_SetsLastAccess()
    {
        // Arrange
        var mockFileSystem = Substitute.For<IPLangFileSystem>();
        mockFileSystem.RootDirectory.Returns("C:\\test");
        var pool = new EnginePoolService(mockFileSystem);

        var mockEngine = Substitute.For<IEngine>();
        mockEngine.Container.Returns(Substitute.For<IServiceContainer>());
        var beforeReturn = DateTime.UtcNow;

        // Act
        pool.Return(mockEngine);

        // Assert
        mockEngine.Received().LastAccess = Arg.Is<DateTime>(d => d >= beforeReturn);
    }

    [Test]
    public async Task Return_ValidEngine_IncreasesPoolCount()
    {
        // Arrange
        var mockFileSystem = Substitute.For<IPLangFileSystem>();
        mockFileSystem.RootDirectory.Returns("C:\\test");
        var pool = new EnginePoolService(mockFileSystem);

        var mockEngine = Substitute.For<IEngine>();
        mockEngine.Container.Returns(Substitute.For<IServiceContainer>());

        var countBefore = pool.AvailableCount;

        // Act
        pool.Return(mockEngine);

        // Assert
        await Assert.That(pool.AvailableCount).IsEqualTo(countBefore + 1);
    }

    [Test]
    public async Task Return_MultipleEngines_IncreasesPoolCount()
    {
        // Arrange
        var mockFileSystem = Substitute.For<IPLangFileSystem>();
        mockFileSystem.RootDirectory.Returns("C:\\test");
        var pool = new EnginePoolService(mockFileSystem);

        var countBefore = pool.AvailableCount;

        // Act
        for (int i = 0; i < 5; i++)
        {
            var mockEngine = Substitute.For<IEngine>();
            mockEngine.Container.Returns(Substitute.For<IServiceContainer>());
            pool.Return(mockEngine);
        }

        // Assert
        await Assert.That(pool.AvailableCount).IsEqualTo(countBefore + 5);
    }

    [Test]
    public async Task Return_WhenResetThrows_DoesNotCrash()
    {
        // Arrange
        var mockFileSystem = Substitute.For<IPLangFileSystem>();
        mockFileSystem.RootDirectory.Returns("C:\\test");
        var pool = new EnginePoolService(mockFileSystem);

        var mockEngine = Substitute.For<IEngine>();
        mockEngine.Container.Returns(Substitute.For<IServiceContainer>());
        mockEngine.When(e => e.Reset()).Throw(new Exception("Reset failed"));

        var countBefore = pool.AvailableCount;

        // Act - should not throw
        pool.Return(mockEngine);

        // Assert - pool count should not increase due to error
        await Assert.That(pool.AvailableCount).IsEqualTo(countBefore);
    }
}

/// <summary>
/// Tests for CallStack HasFrames check.
/// Verifies the fix for "No frame on CallStack" exception.
/// </summary>
public class CallStackFrameTests
{
    [Test]
    public async Task CallStack_HasFrames_IsFalseWhenEmpty()
    {
        // Arrange
        var callStack = new CallStack();

        // Assert
        await Assert.That(callStack.HasFrames).IsFalse();
    }

    [Test]
    public async Task CallStack_HasFrames_IsTrueAfterEnterGoal()
    {
        // Arrange
        var callStack = new CallStack();
        var goal = new Goal { GoalName = "TestGoal" };

        // Act
        callStack.EnterGoal(goal);

        // Assert
        await Assert.That(callStack.HasFrames).IsTrue();
    }

    [Test]
    public async Task CallStack_CurrentFrame_ThrowsWhenNoFrames()
    {
        // Arrange
        var callStack = new CallStack();

        // Act & Assert
        InvalidOperationException? exception = null;
        try
        {
            var _ = callStack.CurrentFrame;
        }
        catch (InvalidOperationException ex)
        {
            exception = ex;
        }

        await Assert.That(exception).IsNotNull();
        await Assert.That(exception!.Message).Contains("No frame on CallStack");
    }

    [Test]
    public async Task CallStack_CurrentFrame_ReturnsFrameAfterEnterGoal()
    {
        // Arrange
        var callStack = new CallStack();
        var goal = new Goal { GoalName = "TestGoal" };

        // Act
        callStack.EnterGoal(goal);

        // Assert
        await Assert.That(callStack.CurrentFrame).IsNotNull();
        await Assert.That(callStack.CurrentFrame.Goal.GoalName).IsEqualTo("TestGoal");
    }

    [Test]
    public async Task CallStack_ExitGoal_ReducesFrameCount()
    {
        // Arrange
        var callStack = new CallStack();
        callStack.EnterGoal(new Goal { GoalName = "Goal1" });
        callStack.EnterGoal(new Goal { GoalName = "Goal2" });

        await Assert.That(callStack.Depth).IsEqualTo(2);

        // Act
        callStack.ExitGoal();

        // Assert
        await Assert.That(callStack.Depth).IsEqualTo(1);
        await Assert.That(callStack.HasFrames).IsTrue();
    }

    [Test]
    public async Task CallStack_ExitGoal_HasFramesFalseWhenAllExited()
    {
        // Arrange
        var callStack = new CallStack();
        callStack.EnterGoal(new Goal { GoalName = "Goal1" });

        // Act
        callStack.ExitGoal();

        // Assert
        await Assert.That(callStack.HasFrames).IsFalse();
    }

    [Test]
    public async Task CallStack_Depth_ReflectsFrameCount()
    {
        // Arrange
        var callStack = new CallStack();

        await Assert.That(callStack.Depth).IsEqualTo(0);

        // Act & Assert
        callStack.EnterGoal(new Goal { GoalName = "Goal1" });
        await Assert.That(callStack.Depth).IsEqualTo(1);

        callStack.EnterGoal(new Goal { GoalName = "Goal2" });
        await Assert.That(callStack.Depth).IsEqualTo(2);

        callStack.EnterGoal(new Goal { GoalName = "Goal3" });
        await Assert.That(callStack.Depth).IsEqualTo(3);
    }

    [Test]
    public async Task CallStack_CurrentGoal_ReturnsCorrectGoal()
    {
        // Arrange
        var callStack = new CallStack();
        var goal1 = new Goal { GoalName = "Goal1" };
        var goal2 = new Goal { GoalName = "Goal2" };

        callStack.EnterGoal(goal1);
        callStack.EnterGoal(goal2);

        // Assert - current should be most recently entered
        await Assert.That(callStack.CurrentGoal.GoalName).IsEqualTo("Goal2");

        // Act
        callStack.ExitGoal();

        // Assert - now current should be first goal
        await Assert.That(callStack.CurrentGoal.GoalName).IsEqualTo("Goal1");
    }
}

/// <summary>
/// Tests for IEnginePool interface contract.
/// </summary>
[NotInParallel]
public class EnginePoolInterfaceTests
{
    [Before(Test)]
    public void Setup()
    {
        EnginePoolService.DisposeAll();
    }

    [After(Test)]
    public void Cleanup()
    {
        EnginePoolService.DisposeAll();
    }

    [Test]
    public async Task IEnginePool_ImplementedByEnginePoolService()
    {
        // Arrange
        var mockFileSystem = Substitute.For<IPLangFileSystem>();
        mockFileSystem.RootDirectory.Returns("C:\\test");

        // Act
        IEnginePool pool = new EnginePoolService(mockFileSystem);

        // Assert
        await Assert.That(pool).IsTypeOf<EnginePoolService>();
    }

    [Test]
    public async Task IEnginePool_Dispose_DoesNotThrow()
    {
        // Arrange
        var mockFileSystem = Substitute.For<IPLangFileSystem>();
        mockFileSystem.RootDirectory.Returns("C:\\test");
        var pool = new EnginePoolService(mockFileSystem);

        // Act & Assert - should not throw
        pool.Dispose();
    }

    [Test]
    public async Task IEnginePool_AvailableCount_IsAccessible()
    {
        // Arrange
        var mockFileSystem = Substitute.For<IPLangFileSystem>();
        mockFileSystem.RootDirectory.Returns("C:\\test");
        IEnginePool pool = new EnginePoolService(mockFileSystem);

        // Act & Assert
        await Assert.That(pool.AvailableCount).IsGreaterThanOrEqualTo(0);
    }

    [Test]
    public async Task IEnginePool_TotalCreated_IsAccessible()
    {
        // Arrange
        var mockFileSystem = Substitute.For<IPLangFileSystem>();
        mockFileSystem.RootDirectory.Returns("C:\\test");
        IEnginePool pool = new EnginePoolService(mockFileSystem);

        // Act & Assert
        await Assert.That(pool.TotalCreated).IsGreaterThanOrEqualTo(0);
    }
}

/// <summary>
/// Tests for concurrent access to engine pool using mocked engines.
/// </summary>
[NotInParallel]
public class EnginePoolConcurrencyTests
{
    [Before(Test)]
    public void Setup()
    {
        EnginePoolService.DisposeAll();
    }

    [After(Test)]
    public void Cleanup()
    {
        EnginePoolService.DisposeAll();
    }

    [Test]
    public async Task ConcurrentReturn_IsThreadSafe()
    {
        // Arrange
        var mockFileSystem = Substitute.For<IPLangFileSystem>();
        mockFileSystem.RootDirectory.Returns("C:\\test");
        var pool = new EnginePoolService(mockFileSystem);

        var countBefore = pool.AvailableCount;

        var engines = new List<IEngine>();
        for (int i = 0; i < 10; i++)
        {
            var mockEngine = Substitute.For<IEngine>();
            mockEngine.Container.Returns(Substitute.For<IServiceContainer>());
            mockEngine.Name.Returns($"Engine-{i}");
            engines.Add(mockEngine);
        }

        // Act - return all engines concurrently
        var tasks = engines.Select(e => Task.Run(() => pool.Return(e)));
        await Task.WhenAll(tasks);

        // Assert - pool should have 10 more engines
        await Assert.That(pool.AvailableCount).IsEqualTo(countBefore + 10);
    }

    [Test]
    public async Task RapidReturn_IsThreadSafe()
    {
        // Arrange
        var mockFileSystem = Substitute.For<IPLangFileSystem>();
        mockFileSystem.RootDirectory.Returns("C:\\test");
        var pool = new EnginePoolService(mockFileSystem);

        var returnCount = 0;
        var tasks = Enumerable.Range(0, 20).Select(_ => Task.Run(() =>
        {
            var mockEngine = Substitute.For<IEngine>();
            mockEngine.Container.Returns(Substitute.For<IServiceContainer>());
            pool.Return(mockEngine);
            Interlocked.Increment(ref returnCount);
        }));

        // Act
        await Task.WhenAll(tasks);

        // Assert
        await Assert.That(returnCount).IsEqualTo(20);
        await Assert.That(pool.AvailableCount).IsGreaterThan(0);
    }
}

/// <summary>
/// Tests verifying bootstrap container correctly registers engine pool.
/// </summary>
[NotInParallel]
public class EnginePoolBootstrapTests
{
    [Before(Test)]
    public void Setup()
    {
        EnginePoolService.DisposeAll();
    }

    [After(Test)]
    public void Cleanup()
    {
        EnginePoolService.DisposeAll();
    }

    [Test]
    public async Task RegisterBootstrap_RegistersIEnginePool()
    {
        // Arrange
        var container = new ServiceContainer();
        var testPath = System.IO.Path.GetTempPath();

        // Act
        container.RegisterBootstrap(testPath, "/");
        var enginePool = container.GetInstance<IEnginePool>();

        // Assert
        await Assert.That(enginePool).IsNotNull();
        await Assert.That(enginePool).IsTypeOf<EnginePoolService>();

        container.Dispose();
    }

    [Test]
    public async Task RegisterBootstrap_RegistersIPseudoRuntime()
    {
        // Arrange
        var container = new ServiceContainer();
        var testPath = System.IO.Path.GetTempPath();

        // Act
        container.RegisterBootstrap(testPath, "/");
        var pseudoRuntime = container.GetInstance<IPseudoRuntime>();

        // Assert
        await Assert.That(pseudoRuntime).IsNotNull();
        await Assert.That(pseudoRuntime).IsTypeOf<PseudoRuntime>();

        container.Dispose();
    }

    [Test]
    public async Task RegisterBootstrap_PseudoRuntimeHasEnginePoolDependency()
    {
        // Arrange
        var container = new ServiceContainer();
        var testPath = System.IO.Path.GetTempPath();

        // Act - both should resolve without error
        container.RegisterBootstrap(testPath, "/");
        var pseudoRuntime = container.GetInstance<IPseudoRuntime>();
        var enginePool = container.GetInstance<IEnginePool>();

        // Assert - if dependencies weren't registered, we'd get exception above
        await Assert.That(pseudoRuntime).IsNotNull();
        await Assert.That(enginePool).IsNotNull();

        container.Dispose();
    }
}

/// <summary>
/// Tests for CallStackFrame variable management.
/// </summary>
public class CallStackFrameVariableTests
{
    [Test]
    public async Task CallStackFrame_AddVariable_StoresValue()
    {
        // Arrange
        var callStack = new CallStack();
        var goal = new Goal { GoalName = "TestGoal" };
        callStack.EnterGoal(goal);

        // Act
        callStack.CurrentFrame.AddVariable("testValue", variableName: "testVar");

        // Assert
        var result = callStack.CurrentFrame.GetVariable("testVar");
        await Assert.That(result).IsEqualTo("testValue");
    }

    [Test]
    public async Task CallStackFrame_GetVariable_ReturnsNullForMissing()
    {
        // Arrange
        var callStack = new CallStack();
        callStack.EnterGoal(new Goal { GoalName = "TestGoal" });

        // Act
        var result = callStack.CurrentFrame.GetVariable("nonexistent");

        // Assert
        await Assert.That(result).IsNull();
    }

    [Test]
    public async Task CallStackFrame_Variables_IsolatedPerFrame()
    {
        // Arrange
        var callStack = new CallStack();

        callStack.EnterGoal(new Goal { GoalName = "Goal1" });
        callStack.CurrentFrame.AddVariable("value1", variableName: "var1");

        callStack.EnterGoal(new Goal { GoalName = "Goal2" });
        callStack.CurrentFrame.AddVariable("value2", variableName: "var2");

        // Assert - inner frame has its own variable
        await Assert.That(callStack.CurrentFrame.GetVariable("var2")).IsEqualTo("value2");

        // Inner frame can see parent's variable
        await Assert.That(callStack.CurrentFrame.GetVariable("var1")).IsEqualTo("value1");

        // Exit inner frame
        callStack.ExitGoal();

        // Outer frame should not see inner frame's variable
        await Assert.That(callStack.CurrentFrame.GetVariable("var2")).IsNull();
        await Assert.That(callStack.CurrentFrame.GetVariable("var1")).IsEqualTo("value1");
    }
}
