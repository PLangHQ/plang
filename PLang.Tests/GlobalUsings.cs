// Engine root (works in tests — App.@this is the engine)
global using App = app.@this;

// Goals subsystem — mirrors PLang/App/GlobalUsings.cs
global using EngineGoals = app.goals.@this;
global using GoalCall = app.goals.goal.GoalCall;
global using GoalSteps = app.goals.goal.steps.@this;
global using Step = app.goals.goal.steps.step.@this;
global using ErrorOrder = app.goals.goal.steps.step.ErrorOrder;
global using CacheSettings = app.goals.goal.steps.step.CacheSettings;
global using StepActions = app.goals.goal.steps.step.actions.@this;
global using PrAction = app.goals.goal.steps.step.actions.action.@this;
global using ActionModifiers = app.goals.goal.steps.step.actions.action.modifiers.@this;

// Types that have v1 conflicts in PLang project but NOT in PLang.Tests (no Building.Model here)
global using Goal = app.goals.goal.@this;
global using Visibility = app.goals.goal.Visibility;

// Event types
global using EventType = app.events.EventType;
global using EngineEvents = app.events.@this;
global using EventBinding = app.events.lifecycle.bindings.binding.@this;
global using Lifecycle = app.events.lifecycle.@this;
global using Bindings = app.events.lifecycle.bindings.@this;

// Modules subsystem (action registry)
global using EngineModules = app.modules.@this;

// Channels subsystem
global using EngineChannels = app.channels.@this;
global using Channel = app.channels.channel.@this;
global using StreamChannel = app.channels.channel.stream.@this;
global using ChannelDirection = app.channels.channel.ChannelDirection;
global using Serializers = app.channels.serializers.@this;
global using SerializeOptions = app.channels.serializers.SerializeOptions;
global using DeserializeOptions = app.channels.serializers.DeserializeOptions;

// Data (universal type)
global using Data = global::app.data.@this;
global using Properties = global::app.data.Properties;
global using DynamicData = global::app.data.DynamicData;
global using TString = global::app.data.TString;

// Variables (was MemoryStack)
global using Variables = app.variables.@this;

// FileSystem types
global using FileSystem = app.filesystem;
global using PLangFileSystem = app.filesystem.Default.PLangFileSystem;

// Type system
global using EngineTypes = app.types.@this;

// Standalone concepts (no v1 conflicts in tests)
global using ICache = app.modules.cache.ICache;
global using CallStack = app.callstack.@this;
global using Flags = app.callstack.Flags;
// Call: not a global alias — App.modules.goal.Call (the goal.call action handler)
// collides. Use App.CallStack.Call.@this fully qualified, or per-file alias.
global using Debugging = app.Debug.@this;
global using Tester = app.tester.@this;
global using Snapshot = app.snapshot.@this;
global using ISnapshot = app.snapshot.ISnapshot;
global using AppStatics = app.Statics.@this;
