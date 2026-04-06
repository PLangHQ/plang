// Engine root (works in tests — no App.Engine namespace to shadow it)
global using Engine = App.Engine.@this;

// Goals subsystem — mirrors PLang/App/GlobalUsings.cs
global using EngineGoals = App.Engine.Goals.@this;
global using GoalCall = App.Engine.Goals.Goal.GoalCall;
global using GoalSteps = App.Engine.Goals.Goal.Steps.@this;
global using Step = App.Engine.Goals.Goal.Steps.Step.@this;
global using ErrorOrder = App.Engine.Goals.Goal.Steps.Step.ErrorOrder;
global using CacheSettings = App.Engine.Goals.Goal.Steps.Step.CacheSettings;
global using StepActions = App.Engine.Goals.Goal.Steps.Step.Actions.@this;

// Types that have v1 conflicts in PLang project but NOT in PLang.Tests (no Building.Model here)
global using Goal = App.Engine.Goals.Goal.@this;
global using Visibility = App.Engine.Goals.Goal.Visibility;
global using ErrorHandler = App.Engine.Goals.Goal.Steps.Step.ErrorHandler;

// Event types
global using EventType = App.Engine.Events.EventType;
global using EngineEvents = App.Engine.Events.@this;
global using EventBinding = App.Engine.Events.Lifecycle.Bindings.Binding.@this;
global using Lifecycle = App.Engine.Events.Lifecycle.@this;
global using Bindings = App.Engine.Events.Lifecycle.Bindings.@this;

// Modules subsystem (action registry)
global using EngineModules = App.Engine.Modules.@this;

// Channels subsystem
global using EngineChannels = App.Engine.Channels.@this;
global using Channel = App.Engine.Channels.Channel.@this;
global using ChannelDirection = App.Engine.Channels.Channel.ChannelDirection;
global using Serializers = App.Engine.Channels.Serializers.@this;
global using SerializeOptions = App.Engine.Channels.Serializers.SerializeOptions;
global using DeserializeOptions = App.Engine.Channels.Serializers.DeserializeOptions;

// Variables (was MemoryStack)
global using Variables = App.Engine.Variables.@this;

// FileSystem types
global using PLangPath = App.Engine.FileSystem.Path;

// Type system
global using EngineTypes = App.Engine.Types.@this;

// Standalone concepts (no v1 conflicts in tests)
global using ICache = App.Engine.Cache.ICache;
global using MemoryStepCache = App.Engine.Cache.MemoryStepCache;
global using CallStack = App.Engine.CallStack.@this;
global using CallFrame = App.Engine.CallStack.CallFrame;
global using ExecutedStep = App.Engine.CallStack.ExecutedStep;
global using SerializableCallStack = App.Engine.CallStack.SerializableCallStack;
global using ExecutionPhase = App.Engine.CallStack.ExecutionPhase;
global using SerializableCallFrame = App.Engine.CallStack.SerializableCallFrame;
global using Debugging = App.Engine.Debug.@this;
global using Testing = App.Engine.Test.@this;
