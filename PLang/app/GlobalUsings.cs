
// Goals subsystem
global using AppGoals = app.goals.@this;
global using GoalCall = app.goals.goal.GoalCall;
global using GoalSteps = app.goals.goal.steps.@this;
global using Step = app.goals.goal.steps.step.@this;
global using ErrorOrder = app.goals.goal.steps.step.ErrorOrder;
global using CacheSettings = app.goals.goal.steps.step.CacheSettings;
global using StepActions = app.goals.goal.steps.step.actions.@this;
global using ActionModifiers = app.goals.goal.steps.step.actions.action.modifiers.@this;

// Event types
global using AppEvents = app.events.@this;
global using Lifecycle = app.events.lifecycle.@this;
global using Bindings = app.events.lifecycle.bindings.@this;

// Event types WITH conflicts — require per-file handling:
// AppEvents alias (not "Events") avoids collision with PLang.Events namespace
// EventType: v1 PLang.Events conflict — use: using App.Events; or per-file alias
// EventBinding: v1 PLang.Events conflict — use: using EventBinding = App.Events.Lifecycle.Bindings.Binding.@this;

// Modules subsystem (action registry)
global using AppModules = app.Modules.@this;

// Channels subsystem
global using AppChannels = app.channels.@this;
global using Channel = app.channels.channel.@this;
global using ChannelDirection = app.channels.channel.ChannelDirection;
global using Serializers = app.channels.serializers.@this;
global using SerializeOptions = app.channels.serializers.SerializeOptions;
global using DeserializeOptions = app.channels.serializers.DeserializeOptions;

// FileSystem types

// Config subsystem
global using AppConfig = app.config.@this;
global using ConfigScope = app.config.Scope;

// Type system
global using AppTypes = app.types.@this;

// Code subsystem (the runtime escape-hatch — was Providers)
global using AppCode = app.Code.@this;

// Variables (was MemoryStack)
global using Variables = app.variables.@this;

// Snapshot subsystem
global using Snapshot = app.snapshot.@this;
global using ISnapshot = app.snapshot.ISnapshot;

// Callback subsystem
global using AppCallback = app.modules.callback.@this;
global using ICallback = app.modules.callback.ICallback;

// Statics — App-scoped key/value store extracted from App._statics
global using AppStatics = app.Statics.@this;

// Standalone concepts
global using ICache = app.modules.cache.ICache;
global using Debugging = app.Debug.@this;
global using Tester = app.tester.@this;

// Call: not a global alias — App.modules.goal.Call (the goal.call action handler) collides.
// Use App.CallStack.Call.@this fully qualified, or per-file alias.

// Builder: can't be global alias — v1 PLang.Builder namespace conflict (legacy).
// Inside App.@this: the `Builder` property shadows the `Builder` namespace, so
// type references must be fully qualified (global::app.modules.builder.@this).
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
