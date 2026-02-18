// Entity types — mirrors PLang/Runtime2/GlobalUsings.cs
global using GoalCall = PLang.Runtime2.Engine.Goals.GoalCall;
global using GoalSteps = PLang.Runtime2.Engine.Goals.Steps.GoalSteps;
global using Step = PLang.Runtime2.Engine.Goals.Steps.Step;
global using ErrorOrder = PLang.Runtime2.Engine.Goals.Steps.ErrorOrder;
global using CacheSettings = PLang.Runtime2.Engine.Goals.Steps.CacheSettings;
global using StepCache = PLang.Runtime2.Engine.Goals.Steps.StepCache;
global using StepActions = PLang.Runtime2.Engine.Goals.Steps.Actions.StepActions;
global using IAction = PLang.Runtime2.Engine.Goals.Steps.Actions.IAction;

// Types that have v1 conflicts in PLang project but NOT in PLang.Tests (no Building.Model here)
global using Goal = PLang.Runtime2.Engine.Goals.Goal;
global using Visibility = PLang.Runtime2.Engine.Goals.Visibility;
global using ErrorHandler = PLang.Runtime2.Engine.Goals.Steps.ErrorHandler;

// Event types
global using EventType = PLang.Runtime2.Engine.Events.EventType;
global using EngineEvents = PLang.Runtime2.Engine.Events.EngineEvents;
global using EventBinding = PLang.Runtime2.Engine.Events.EventBinding;
global using Lifecycle = PLang.Runtime2.Engine.Events.Lifecycle;
global using Bindings = PLang.Runtime2.Engine.Events.Bindings;

// Standalone concepts (no v1 conflicts in tests)
global using EngineLibraries = PLang.Runtime2.Engine.Libraries.EngineLibraries;
global using Library = PLang.Runtime2.Engine.Libraries.Library;
global using ICache = PLang.Runtime2.Engine.Cache.ICache;
global using MemoryStepCache = PLang.Runtime2.Engine.Cache.MemoryStepCache;
global using StepCacheEntry = PLang.Runtime2.Engine.Cache.StepCacheEntry;
global using CallStack = PLang.Runtime2.Engine.CallStack.CallStack;
global using CallFrame = PLang.Runtime2.Engine.CallStack.CallFrame;
global using ExecutedStep = PLang.Runtime2.Engine.CallStack.ExecutedStep;
global using SerializableCallStack = PLang.Runtime2.Engine.CallStack.SerializableCallStack;
global using ExecutionPhase = PLang.Runtime2.Engine.CallStack.ExecutionPhase;
global using SerializableCallFrame = PLang.Runtime2.Engine.CallStack.SerializableCallFrame;
global using CachedVariable = PLang.Runtime2.Engine.Cache.CachedVariable;
global using EngineDebug = PLang.Runtime2.Engine.Debug.EngineDebug;
global using EngineTesting = PLang.Runtime2.Engine.Test.EngineTesting;
global using EngineProperty = PLang.Runtime2.Engine.Properties.EngineProperty;
