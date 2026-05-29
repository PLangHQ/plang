// Engine root (works in tests — App.@this is the engine)
global using App = app.@this;

// Goals subsystem — mirrors PLang/App/GlobalUsings.cs
global using GoalCall = app.goal.GoalCall;
global using GoalSteps = app.goal.steps.@this;
global using Step = app.goal.steps.step.@this;
global using ErrorOrder = app.goal.steps.step.ErrorOrder;
global using CacheSettings = app.goal.steps.step.CacheSettings;
global using StepActions = app.goal.steps.step.actions.@this;
global using PrAction = app.goal.steps.step.actions.action.@this;
global using ActionModifiers = app.goal.steps.step.actions.action.modifiers.@this;

// Types that have v1 conflicts in PLang project but NOT in PLang.Tests (no Building.Model here)
global using Goal = app.goal.@this;
global using Visibility = app.goal.Visibility;

// Event types
global using EventType = app.@event.EventType;
global using EventBinding = app.@event.lifecycle.binding.@this;
global using Lifecycle = app.@event.lifecycle.@this;
global using Bindings = app.@event.lifecycle.binding.list.@this;

// Modules subsystem (action registry)

// Channels subsystem
global using Channel = app.channel.@this;
global using StreamChannel = app.channel.stream.@this;
global using ChannelDirection = app.channel.ChannelDirection;
global using Serializers = app.channel.serializer.list.@this;
global using SerializeOptions = app.channel.serializer.list.SerializeOptions;
global using DeserializeOptions = app.channel.serializer.list.DeserializeOptions;

// Data (universal type)
global using Data = global::app.data.@this;
global using Properties = global::app.data.Properties;
global using DynamicData = global::app.data.DynamicData;
global using TString = global::app.data.TString;

// Variables (was MemoryStack)
global using Variables = app.variable.list.@this;

// Path types (formerly FileSystem)
global using FileSystem = app.type.path;
// Lowercase path aliases — match PLang concept names, no System.IO.Path clash.
global using path = app.type.path.@this;
global using filepath = app.type.path.file.@this;
global using httppath = app.type.path.http.@this;

// Type system

// Standalone concepts (no v1 conflicts in tests)
global using ICache = app.module.cache.ICache;
global using CallStack = app.callstack.@this;
global using Flags = app.callstack.Flags;
// Call: not a global alias — app.module.goal.Call (the goal.call action handler)
// collides. Use app.callstack.call.@this fully qualified, or per-file alias.
global using Debugging = app.module.debug.@this;
global using Tester = app.tester.@this;
global using Snapshot = app.snapshot.@this;
global using ISnapshot = app.snapshot.ISnapshot;
global using AppStatics = app.Statics.@this;
