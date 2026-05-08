
// Goals subsystem
global using AppGoals = App.Goals.@this;
global using GoalCall = App.Goals.Goal.GoalCall;
global using GoalSteps = App.Goals.Goal.Steps.@this;
global using Step = App.Goals.Goal.Steps.Step.@this;
global using ErrorOrder = App.Goals.Goal.Steps.Step.ErrorOrder;
global using CacheSettings = App.Goals.Goal.Steps.Step.CacheSettings;
global using StepActions = App.Goals.Goal.Steps.Step.Actions.@this;
global using ActionModifiers = App.Goals.Goal.Steps.Step.Actions.Action.Modifiers.@this;

// Event types
global using AppEvents = App.Events.@this;
global using Lifecycle = App.Events.Lifecycle.@this;
global using Bindings = App.Events.Lifecycle.Bindings.@this;

// Event types WITH conflicts — require per-file handling:
// AppEvents alias (not "Events") avoids collision with PLang.Events namespace
// EventType: v1 PLang.Events conflict — use: using App.Events; or per-file alias
// EventBinding: v1 PLang.Events conflict — use: using EventBinding = App.Events.Lifecycle.Bindings.Binding.@this;

// Modules subsystem (action registry)
global using AppModules = App.Modules.@this;

// Channels subsystem
global using AppChannels = App.Channels.@this;
global using Channel = App.Channels.Channel.@this;
global using ChannelDirection = App.Channels.Channel.ChannelDirection;
global using Serializers = App.Channels.Serializers.@this;
global using SerializeOptions = App.Channels.Serializers.SerializeOptions;
global using DeserializeOptions = App.Channels.Serializers.DeserializeOptions;

// FileSystem types

// Config subsystem
global using AppConfig = App.Config.@this;
global using ConfigScope = App.Config.Scope;

// Type system
global using AppTypes = App.Types.@this;

// Providers subsystem
global using AppProviders = App.Providers.@this;

// Variables (was MemoryStack)
global using Variables = App.Variables.@this;

// Snapshot subsystem
global using Snapshot = App.Snapshot.@this;
global using ISnapshotted = App.Snapshot.ISnapshotted;

// Callback subsystem
global using AppCallback = App.Callback.@this;
global using ICallback = App.Callback.ICallback;

// Statics — App-scoped key/value store extracted from App._statics
global using AppStatics = App.Statics.@this;

// Standalone concepts
global using ICache = App.Cache.ICache;
global using MemoryStepCache = App.Cache.MemoryStepCache;
global using Debugging = App.Debug.@this;
global using Tester = App.Tester.@this;

// Call: not a global alias — App.modules.goal.Call (the goal.call action handler) collides.
// Use App.CallStack.Call.@this fully qualified, or per-file alias.

// Builder: can't be global alias — v1 PLang.Builder namespace conflict (legacy).
// Inside App.@this: the `Builder` property shadows the `Builder` namespace, so
// type references must be fully qualified (global::App.Builder.@this).
// Inside other App.*: Builder.@this resolves naturally.
// Outside App.*: use App.Builder.@this or a per-file alias.

// App: can't be global alias — App.@this IS the app root
// Inside App.*: use App.@this (parent namespace resolves naturally)
// Outside App.*: use App.@this or per-file alias

// CallStack: can't be global alias — v1 PLang.Runtime.CallStack conflict
// Inside App.*: use CallStack.@this (namespace resolves to child)
// Outside App.*: use App.CallStack.@this or per-file alias

// Types WITH v1 conflicts — require per-file handling:
// Goal: use Goals.Goal.@this or per-file alias
// Visibility: use Goals.Goal.Visibility
// Action: can't global alias (System.Action conflict), use per-file alias
