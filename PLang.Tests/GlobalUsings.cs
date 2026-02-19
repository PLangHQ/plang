// Engine root (works in tests — no PLang.Runtime2.Engine namespace to shadow it)
global using Engine = PLang.Runtime2.Engine.@this;

// Goals subsystem — mirrors PLang/Runtime2/GlobalUsings.cs
global using EngineGoals = PLang.Runtime2.Engine.Goals.@this;
global using GoalCall = PLang.Runtime2.Engine.Goals.Goal.GoalCall;
global using GoalSteps = PLang.Runtime2.Engine.Goals.Goal.Steps.@this;
global using Step = PLang.Runtime2.Engine.Goals.Goal.Steps.Step.@this;
global using ErrorOrder = PLang.Runtime2.Engine.Goals.Goal.Steps.Step.ErrorOrder;
global using CacheSettings = PLang.Runtime2.Engine.Goals.Goal.Steps.Step.CacheSettings;
global using StepCache = PLang.Runtime2.Engine.Goals.Goal.Steps.Step.StepCache;
global using StepActions = PLang.Runtime2.Engine.Goals.Goal.Steps.Step.Actions.@this;
global using IAction = PLang.Runtime2.Engine.Goals.Goal.Steps.Step.Actions.Action.IAction;

// Types that have v1 conflicts in PLang project but NOT in PLang.Tests (no Building.Model here)
global using Goal = PLang.Runtime2.Engine.Goals.Goal.@this;
global using Visibility = PLang.Runtime2.Engine.Goals.Goal.Visibility;
global using ErrorHandler = PLang.Runtime2.Engine.Goals.Goal.Steps.Step.ErrorHandler;

// Event types
global using EventType = PLang.Runtime2.Engine.Events.EventType;
global using EngineEvents = PLang.Runtime2.Engine.Events.@this;
global using EventBinding = PLang.Runtime2.Engine.Events.Lifecycle.Bindings.Binding.@this;
global using Lifecycle = PLang.Runtime2.Engine.Events.Lifecycle.@this;
global using Bindings = PLang.Runtime2.Engine.Events.Lifecycle.Bindings.@this;

// Libraries subsystem
global using EngineLibraries = PLang.Runtime2.Engine.Libraries.@this;
global using Library = PLang.Runtime2.Engine.Libraries.Library.@this;

// Channels subsystem
global using EngineChannels = PLang.Runtime2.Engine.Channels.@this;
global using Channel = PLang.Runtime2.Engine.Channels.Channel.@this;
global using ChannelDirection = PLang.Runtime2.Engine.Channels.Channel.ChannelDirection;
global using Serializers = PLang.Runtime2.Engine.Channels.Serializers.@this;
global using SerializeOptions = PLang.Runtime2.Engine.Channels.Serializers.SerializeOptions;
global using DeserializeOptions = PLang.Runtime2.Engine.Channels.Serializers.DeserializeOptions;

// Memory types
global using PLangPath = PLang.Runtime2.Engine.Memory.Path;

// Standalone concepts (no v1 conflicts in tests)
global using ICache = PLang.Runtime2.Engine.Cache.ICache;
global using MemoryStepCache = PLang.Runtime2.Engine.Cache.MemoryStepCache;
global using StepCacheEntry = PLang.Runtime2.Engine.Cache.StepCacheEntry;
global using CallStack = PLang.Runtime2.Engine.CallStack.@this;
global using CallFrame = PLang.Runtime2.Engine.CallStack.CallFrame;
global using ExecutedStep = PLang.Runtime2.Engine.CallStack.ExecutedStep;
global using SerializableCallStack = PLang.Runtime2.Engine.CallStack.SerializableCallStack;
global using ExecutionPhase = PLang.Runtime2.Engine.CallStack.ExecutionPhase;
global using SerializableCallFrame = PLang.Runtime2.Engine.CallStack.SerializableCallFrame;
global using CachedVariable = PLang.Runtime2.Engine.Cache.CachedVariable;
global using Debugging = PLang.Runtime2.Engine.Debug.@this;
global using Testing = PLang.Runtime2.Engine.Test.@this;
global using Property = PLang.Runtime2.Engine.Properties.@this;
