// Goals subsystem
global using EngineGoals = App.Goals.@this;
global using GoalCall = App.Goals.Goal.GoalCall;
global using GoalSteps = App.Goals.Goal.Steps.@this;
global using Step = App.Goals.Goal.Steps.Step.@this;
global using ErrorOrder = App.Goals.Goal.Steps.Step.ErrorOrder;
global using CacheSettings = App.Goals.Goal.Steps.Step.CacheSettings;
global using StepActions = App.Goals.Goal.Steps.Step.Actions.@this;

// Event types
global using EngineEvents = App.Events.@this;
global using Lifecycle = App.Events.Lifecycle.@this;
global using Bindings = App.Events.Lifecycle.Bindings.@this;

// Event types WITH conflicts — require per-file handling:
// EngineEvents alias (not "Events") avoids collision with PLang.Events namespace
// EventType: v1 PLang.Events conflict — use: using App.Events; or per-file alias
// EventBinding: v1 PLang.Events conflict — use: using EventBinding = App.Events.Lifecycle.Bindings.Binding.@this;

// Modules subsystem (action registry)
global using EngineModules = App.Modules.@this;

// Channels subsystem
global using EngineChannels = App.Channels.@this;
global using Channel = App.Channels.Channel.@this;
global using ChannelDirection = App.Channels.Channel.ChannelDirection;
global using Serializers = App.Channels.Serializers.@this;
global using SerializeOptions = App.Channels.Serializers.SerializeOptions;
global using DeserializeOptions = App.Channels.Serializers.DeserializeOptions;

// FileSystem types

// Config subsystem
global using EngineConfig = App.Config.@this;
global using ConfigScope = App.Config.Scope;

// Type system
global using EngineTypes = App.Types.@this;

// Providers subsystem
global using EngineProviders = App.Providers.@this;

// Variables (was MemoryStack)
global using Variables = App.Variables.@this;

// Standalone concepts
global using ICache = App.Cache.ICache;
global using MemoryStepCache = App.Cache.MemoryStepCache;
global using CallFrame = App.CallStack.CallFrame;
global using Debugging = App.Debug.@this;
global using Testing = App.Test.@this;

// Building: can't be global alias — v1 PLang.Building namespace conflict
// Inside Engine.*: use Build.@this (child namespace resolves naturally)
// Outside Engine.*: use App.Build.@this or per-file alias

// Engine: can't be global alias — App.@this IS the app root
// Inside Engine.*: use App.@this (parent namespace resolves naturally)
// Outside App.*: use App.@this or per-file alias

// CallStack: can't be global alias — v1 PLang.Runtime.CallStack conflict
// Inside Engine.*: use CallStack.@this (namespace resolves to child)
// Outside Engine.*: use App.CallStack.@this or per-file alias

// Types WITH v1 conflicts — require per-file handling:
// Goal: use Goals.Goal.@this or per-file alias
// Visibility: use Goals.Goal.Visibility
// ErrorHandler: use Goals.Goal.Steps.Step.ErrorHandler
// Action: can't global alias (System.Action conflict), use per-file alias
