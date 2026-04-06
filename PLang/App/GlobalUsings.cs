// Goals subsystem
global using EngineGoals = App.Engine.Goals.@this;
global using GoalCall = App.Engine.Goals.Goal.GoalCall;
global using GoalSteps = App.Engine.Goals.Goal.Steps.@this;
global using Step = App.Engine.Goals.Goal.Steps.Step.@this;
global using ErrorOrder = App.Engine.Goals.Goal.Steps.Step.ErrorOrder;
global using CacheSettings = App.Engine.Goals.Goal.Steps.Step.CacheSettings;
global using StepActions = App.Engine.Goals.Goal.Steps.Step.Actions.@this;

// Event types
global using EngineEvents = App.Engine.Events.@this;
global using Lifecycle = App.Engine.Events.Lifecycle.@this;
global using Bindings = App.Engine.Events.Lifecycle.Bindings.@this;

// Event types WITH conflicts — require per-file handling:
// EngineEvents alias (not "Events") avoids collision with PLang.Events namespace
// EventType: v1 PLang.Events conflict — use: using App.Engine.Events; or per-file alias
// EventBinding: v1 PLang.Events conflict — use: using EventBinding = App.Engine.Events.Lifecycle.Bindings.Binding.@this;

// Modules subsystem (action registry)
global using EngineModules = App.Engine.Modules.@this;

// Channels subsystem
global using EngineChannels = App.Engine.Channels.@this;
global using Channel = App.Engine.Channels.Channel.@this;
global using ChannelDirection = App.Engine.Channels.Channel.ChannelDirection;
global using Serializers = App.Engine.Channels.Serializers.@this;
global using SerializeOptions = App.Engine.Channels.Serializers.SerializeOptions;
global using DeserializeOptions = App.Engine.Channels.Serializers.DeserializeOptions;

// FileSystem types
global using PLangPath = App.Engine.FileSystem.Path;

// Config subsystem
global using EngineConfig = App.Engine.Config.@this;
global using ConfigScope = App.Engine.Config.Scope;

// Type system
global using EngineTypes = App.Engine.Types.@this;

// Providers subsystem
global using EngineProviders = App.Engine.Providers.@this;

// Variables (was MemoryStack)
global using Variables = App.Engine.Variables.@this;

// Standalone concepts
global using ICache = App.Engine.Cache.ICache;
global using MemoryStepCache = App.Engine.Cache.MemoryStepCache;
global using CallFrame = App.Engine.CallStack.CallFrame;
global using Debugging = App.Engine.Debug.@this;
global using Testing = App.Engine.Test.@this;

// Building: can't be global alias — v1 PLang.Building namespace conflict
// Inside Engine.*: use Build.@this (child namespace resolves naturally)
// Outside Engine.*: use App.Engine.Build.@this or per-file alias

// Engine: can't be global alias — namespace App.Engine shadows it from all App.* files
// Inside Engine.*: use Engine.@this (parent namespace resolves naturally)
// Outside App.*: use App.Engine.@this or per-file alias

// CallStack: can't be global alias — v1 PLang.Runtime.CallStack conflict
// Inside Engine.*: use CallStack.@this (namespace resolves to child)
// Outside Engine.*: use App.Engine.CallStack.@this or per-file alias

// Types WITH v1 conflicts — require per-file handling:
// Goal: use Goals.Goal.@this or per-file alias
// Visibility: use Goals.Goal.Visibility
// ErrorHandler: use Goals.Goal.Steps.Step.ErrorHandler
// Action: can't global alias (System.Action conflict), use per-file alias
