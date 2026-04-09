// Engine root (works in tests — App.@this is the engine)
global using App = App.@this;

// Goals subsystem — mirrors PLang/App/GlobalUsings.cs
global using EngineGoals = App.Goals.@this;
global using GoalCall = App.Goals.Goal.GoalCall;
global using GoalSteps = App.Goals.Goal.Steps.@this;
global using Step = App.Goals.Goal.Steps.Step.@this;
global using ErrorOrder = App.Goals.Goal.Steps.Step.ErrorOrder;
global using CacheSettings = App.Goals.Goal.Steps.Step.CacheSettings;
global using StepActions = App.Goals.Goal.Steps.Step.Actions.@this;
global using PrAction = App.Goals.Goal.Steps.Step.Actions.Action.@this;

// Types that have v1 conflicts in PLang project but NOT in PLang.Tests (no Building.Model here)
global using Goal = App.Goals.Goal.@this;
global using Visibility = App.Goals.Goal.Visibility;
global using ErrorHandler = App.Goals.Goal.Steps.Step.ErrorHandler;

// Event types
global using EventType = App.Events.EventType;
global using EngineEvents = App.Events.@this;
global using EventBinding = App.Events.Lifecycle.Bindings.Binding.@this;
global using Lifecycle = App.Events.Lifecycle.@this;
global using Bindings = App.Events.Lifecycle.Bindings.@this;

// Modules subsystem (action registry)
global using EngineModules = App.Modules.@this;

// Channels subsystem
global using EngineChannels = App.Channels.@this;
global using Channel = App.Channels.Channel.@this;
global using ChannelDirection = App.Channels.Channel.ChannelDirection;
global using Serializers = App.Channels.Serializers.@this;
global using SerializeOptions = App.Channels.Serializers.SerializeOptions;
global using DeserializeOptions = App.Channels.Serializers.DeserializeOptions;

// Data (universal type)
global using Data = global::App.Data.@this;
global using Properties = global::App.Data.Properties;
global using DynamicData = global::App.Data.DynamicData;
global using TString = global::App.Data.TString;

// Variables (was MemoryStack)
global using Variables = App.Variables.@this;

// FileSystem types

// Type system
global using EngineTypes = App.Types.@this;

// Standalone concepts (no v1 conflicts in tests)
global using ICache = App.Cache.ICache;
global using MemoryStepCache = App.Cache.MemoryStepCache;
global using CallStack = App.CallStack.@this;
global using CallFrame = App.CallStack.CallFrame;
global using SerializableCallStack = App.CallStack.SerializableCallStack;
global using ExecutionPhase = App.CallStack.ExecutionPhase;
global using SerializableCallFrame = App.CallStack.SerializableCallFrame;
global using Debugging = App.Debug.@this;
global using Testing = App.Test.@this;
