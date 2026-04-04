// Goals subsystem
global using EngineGoals = PLang.Runtime2.Engine.Goals.@this;
global using GoalCall = PLang.Runtime2.Engine.Goals.Goal.GoalCall;
global using GoalSteps = PLang.Runtime2.Engine.Goals.Goal.Steps.@this;
global using Step = PLang.Runtime2.Engine.Goals.Goal.Steps.Step.@this;
global using ErrorOrder = PLang.Runtime2.Engine.Goals.Goal.Steps.Step.ErrorOrder;
global using CacheSettings = PLang.Runtime2.Engine.Goals.Goal.Steps.Step.CacheSettings;
global using StepActions = PLang.Runtime2.Engine.Goals.Goal.Steps.Step.Actions.@this;

// Event types
global using EngineEvents = PLang.Runtime2.Engine.Events.@this;
global using Lifecycle = PLang.Runtime2.Engine.Events.Lifecycle.@this;
global using Bindings = PLang.Runtime2.Engine.Events.Lifecycle.Bindings.@this;

// Event types WITH conflicts — require per-file handling:
// EngineEvents alias (not "Events") avoids collision with PLang.Events namespace
// EventType: v1 PLang.Events conflict — use: using PLang.Runtime2.Engine.Events; or per-file alias
// EventBinding: v1 PLang.Events conflict — use: using EventBinding = PLang.Runtime2.Engine.Events.Lifecycle.Bindings.Binding.@this;

// Modules subsystem (action registry)
global using EngineModules = PLang.Runtime2.Engine.Modules.@this;

// Channels subsystem
global using EngineChannels = PLang.Runtime2.Engine.Channels.@this;
global using Channel = PLang.Runtime2.Engine.Channels.Channel.@this;
global using ChannelDirection = PLang.Runtime2.Engine.Channels.Channel.ChannelDirection;
global using Serializers = PLang.Runtime2.Engine.Channels.Serializers.@this;
global using SerializeOptions = PLang.Runtime2.Engine.Channels.Serializers.SerializeOptions;
global using DeserializeOptions = PLang.Runtime2.Engine.Channels.Serializers.DeserializeOptions;

// FileSystem types
global using PLangPath = PLang.Runtime2.Engine.FileSystem.Path;

// Config subsystem
global using EngineConfig = PLang.Runtime2.Engine.Config.@this;
global using ConfigScope = PLang.Runtime2.Engine.Config.Scope;

// Type system
global using EngineTypes = PLang.Runtime2.Engine.Types.@this;

// Providers subsystem
global using EngineProviders = PLang.Runtime2.Engine.Providers.@this;

// Standalone concepts
global using ICache = PLang.Runtime2.Engine.Cache.ICache;
global using MemoryStepCache = PLang.Runtime2.Engine.Cache.MemoryStepCache;
global using CallFrame = PLang.Runtime2.Engine.CallStack.CallFrame;
global using Debugging = PLang.Runtime2.Engine.Debug.@this;
global using Testing = PLang.Runtime2.Engine.Test.@this;

// Building: can't be global alias — v1 PLang.Building namespace conflict
// Inside Engine.*: use Build.@this (child namespace resolves naturally)
// Outside Engine.*: use PLang.Runtime2.Engine.Build.@this or per-file alias

// Engine: can't be global alias — namespace PLang.Runtime2.Engine shadows it from all PLang.Runtime2.* files
// Inside Engine.*: use Engine.@this (parent namespace resolves naturally)
// Outside PLang.Runtime2.*: use PLang.Runtime2.Engine.@this or per-file alias

// CallStack: can't be global alias — v1 PLang.Runtime.CallStack conflict
// Inside Engine.*: use CallStack.@this (namespace resolves to child)
// Outside Engine.*: use PLang.Runtime2.Engine.CallStack.@this or per-file alias

// Types WITH v1 conflicts — require per-file handling:
// Goal: use Goals.Goal.@this or per-file alias
// Visibility: use Goals.Goal.Visibility
// ErrorHandler: use Goals.Goal.Steps.Step.ErrorHandler
// Action: can't global alias (System.Action conflict), use per-file alias
