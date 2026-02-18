// Goals subsystem
global using EngineGoals = PLang.Runtime2.Engine.Goals.@this;
global using GoalCall = PLang.Runtime2.Engine.Goals.Goal.GoalCall;
global using GoalSteps = PLang.Runtime2.Engine.Goals.Goal.Steps.@this;
global using Step = PLang.Runtime2.Engine.Goals.Goal.Steps.Step.@this;
global using ErrorOrder = PLang.Runtime2.Engine.Goals.Goal.Steps.Step.ErrorOrder;
global using CacheSettings = PLang.Runtime2.Engine.Goals.Goal.Steps.Step.CacheSettings;
global using StepCache = PLang.Runtime2.Engine.Goals.Goal.Steps.Step.StepCache;
global using StepActions = PLang.Runtime2.Engine.Goals.Goal.Steps.Step.Actions.@this;
global using IAction = PLang.Runtime2.Engine.Goals.Goal.Steps.Step.Actions.Action.IAction;

// Event types
global using EngineEvents = PLang.Runtime2.Engine.Events.@this;
global using Lifecycle = PLang.Runtime2.Engine.Events.Lifecycle.@this;
global using Bindings = PLang.Runtime2.Engine.Events.Lifecycle.Bindings.@this;

// Event types WITH conflicts — require per-file handling:
// EngineEvents alias (not "Events") avoids collision with PLang.Events namespace
// EventType: v1 PLang.Events conflict — use: using PLang.Runtime2.Engine.Events; or per-file alias
// EventBinding: v1 PLang.Events conflict — use: using EventBinding = PLang.Runtime2.Engine.Events.Lifecycle.Bindings.Binding.@this;

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

// Standalone concepts
global using ICache = PLang.Runtime2.Engine.Cache.ICache;
global using MemoryStepCache = PLang.Runtime2.Engine.Cache.MemoryStepCache;
global using StepCacheEntry = PLang.Runtime2.Engine.Cache.StepCacheEntry;
global using CallFrame = PLang.Runtime2.Engine.CallStack.CallFrame;
global using Debugging = PLang.Runtime2.Engine.Debug.@this;
global using Testing = PLang.Runtime2.Engine.Test.@this;
global using Property = PLang.Runtime2.Engine.Properties.@this;

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
