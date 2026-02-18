// Entity types — global aliases for types WITHOUT v1 (Building.Model) naming conflicts
global using GoalCall = PLang.Runtime2.Engine.Goals.GoalCall;
global using GoalSteps = PLang.Runtime2.Engine.Goals.Steps.GoalSteps;
global using Step = PLang.Runtime2.Engine.Goals.Steps.Step;
global using ErrorOrder = PLang.Runtime2.Engine.Goals.Steps.ErrorOrder;
global using CacheSettings = PLang.Runtime2.Engine.Goals.Steps.CacheSettings;
global using StepCache = PLang.Runtime2.Engine.Goals.Steps.StepCache;
global using StepActions = PLang.Runtime2.Engine.Goals.Steps.Actions.StepActions;
global using IAction = PLang.Runtime2.Engine.Goals.Steps.Actions.IAction;

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

// CallStack: namespace-alias collision in Engine.* files — use per-file alias:
//   using R2CallStack = PLang.Runtime2.Engine.CallStack.@this;

// Types WITH v1 conflicts — require per-file handling:
// Goal: use "using PLang.Runtime2.Engine.Goals;" or per-file alias
// Visibility: use "using PLang.Runtime2.Engine.Goals;" or qualified reference
// ErrorHandler: use "using PLang.Runtime2.Engine.Goals.Steps;" or per-file alias
// Action: can't alias (System.Action conflict), use per-file alias
