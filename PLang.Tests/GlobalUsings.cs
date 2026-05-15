// Engine root (works in tests — App.@this is the engine)
global using App = app.@this;

// Goals subsystem — mirrors PLang/App/GlobalUsings.cs
global using EngineGoals = app.Goals.@this;
global using GoalCall = app.Goals.Goal.GoalCall;
global using GoalSteps = app.Goals.Goal.Steps.@this;
global using Step = app.Goals.Goal.Steps.Step.@this;
global using ErrorOrder = app.Goals.Goal.Steps.Step.ErrorOrder;
global using CacheSettings = app.Goals.Goal.Steps.Step.CacheSettings;
global using StepActions = app.Goals.Goal.Steps.Step.Actions.@this;
global using PrAction = app.Goals.Goal.Steps.Step.Actions.Action.@this;
global using ActionModifiers = app.Goals.Goal.Steps.Step.Actions.Action.Modifiers.@this;

// Types that have v1 conflicts in PLang project but NOT in PLang.Tests (no Building.Model here)
global using Goal = app.Goals.Goal.@this;
global using Visibility = app.Goals.Goal.Visibility;

// Event types
global using EventType = app.Events.EventType;
global using EngineEvents = app.Events.@this;
global using EventBinding = app.Events.Lifecycle.Bindings.Binding.@this;
global using Lifecycle = app.Events.Lifecycle.@this;
global using Bindings = app.Events.Lifecycle.Bindings.@this;

// Modules subsystem (action registry)
global using EngineModules = app.Modules.@this;

// Channels subsystem
global using EngineChannels = app.Channels.@this;
global using Channel = app.Channels.Channel.@this;
global using StreamChannel = app.Channels.Channel.Stream.@this;
global using ChannelDirection = app.Channels.Channel.ChannelDirection;
global using Serializers = app.Channels.Serializers.@this;
global using SerializeOptions = app.Channels.Serializers.SerializeOptions;
global using DeserializeOptions = app.Channels.Serializers.DeserializeOptions;

// Data (universal type)
global using Data = global::app.Data.@this;
global using Properties = global::app.Data.Properties;
global using DynamicData = global::app.Data.DynamicData;
global using TString = global::app.Data.TString;

// Variables (was MemoryStack)
global using Variables = app.Variables.@this;

// FileSystem types
global using FileSystem = app.FileSystem;
global using PLangFileSystem = app.FileSystem.Default.PLangFileSystem;

// Type system
global using EngineTypes = app.Types.@this;

// Standalone concepts (no v1 conflicts in tests)
global using ICache = app.Cache.ICache;
global using CallStack = app.CallStack.@this;
global using Flags = app.CallStack.Flags;
// Call: not a global alias — App.modules.goal.Call (the goal.call action handler)
// collides. Use App.CallStack.Call.@this fully qualified, or per-file alias.
global using Debugging = app.Debug.@this;
global using Tester = app.Tester.@this;
global using Snapshot = app.Snapshot.@this;
global using ISnapshot = app.Snapshot.ISnapshot;
global using AppStatics = app.Statics.@this;
