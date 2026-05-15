
// Goals subsystem
global using AppGoals = app.Goals.@this;
global using GoalCall = app.Goals.Goal.GoalCall;
global using GoalSteps = app.Goals.Goal.Steps.@this;
global using Step = app.Goals.Goal.Steps.Step.@this;
global using ErrorOrder = app.Goals.Goal.Steps.Step.ErrorOrder;
global using CacheSettings = app.Goals.Goal.Steps.Step.CacheSettings;
global using StepActions = app.Goals.Goal.Steps.Step.Actions.@this;
global using ActionModifiers = app.Goals.Goal.Steps.Step.Actions.Action.Modifiers.@this;

// Event types
global using AppEvents = app.Events.@this;
global using Lifecycle = app.Events.Lifecycle.@this;
global using Bindings = app.Events.Lifecycle.Bindings.@this;

// Event types WITH conflicts — require per-file handling:
// AppEvents alias (not "Events") avoids collision with PLang.Events namespace
// EventType: v1 PLang.Events conflict — use: using App.Events; or per-file alias
// EventBinding: v1 PLang.Events conflict — use: using EventBinding = App.Events.Lifecycle.Bindings.Binding.@this;

// Modules subsystem (action registry)
global using AppModules = app.Modules.@this;

// Channels subsystem
global using AppChannels = app.Channels.@this;
global using Channel = app.Channels.Channel.@this;
global using ChannelDirection = app.Channels.Channel.ChannelDirection;
global using Serializers = app.Channels.Serializers.@this;
global using SerializeOptions = app.Channels.Serializers.SerializeOptions;
global using DeserializeOptions = app.Channels.Serializers.DeserializeOptions;

// FileSystem types

// Config subsystem
global using AppConfig = app.Config.@this;
global using ConfigScope = app.Config.Scope;

// Type system
global using AppTypes = app.Types.@this;

// Code subsystem (the runtime escape-hatch — was Providers)
global using AppCode = app.Code.@this;

// Variables (was MemoryStack)
global using Variables = app.Variables.@this;

// Snapshot subsystem
global using Snapshot = app.Snapshot.@this;
global using ISnapshot = app.Snapshot.ISnapshot;

// Callback subsystem
global using AppCallback = app.Callback.@this;
global using ICallback = app.Callback.ICallback;

// Statics — App-scoped key/value store extracted from App._statics
global using AppStatics = app.Statics.@this;

// Standalone concepts
global using ICache = app.Cache.ICache;
global using Debugging = app.Debug.@this;
global using Tester = app.Tester.@this;

// Call: not a global alias — App.modules.goal.Call (the goal.call action handler) collides.
// Use App.CallStack.Call.@this fully qualified, or per-file alias.

// Builder: can't be global alias — v1 PLang.Builder namespace conflict (legacy).
// Inside App.@this: the `Builder` property shadows the `Builder` namespace, so
// type references must be fully qualified (global::app.Builder.@this).
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
