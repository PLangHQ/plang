
// Goals subsystem
global using AppGoals = app.goal.list.@this;
global using GoalCall = app.goal.GoalCall;
global using GoalSteps = app.goal.steps.@this;
global using Step = app.goal.steps.step.@this;
global using ErrorOrder = app.goal.steps.step.ErrorOrder;
global using CacheSettings = app.goal.steps.step.CacheSettings;
global using StepActions = app.goal.steps.step.actions.@this;
global using ActionModifiers = app.goal.steps.step.actions.action.modifiers.@this;

// Event types
global using AppEvents = app.@event.list.@this;
global using Lifecycle = app.@event.lifecycle.@this;
global using Bindings = app.@event.lifecycle.binding.list.@this;

// Event types WITH conflicts — require per-file handling:
// AppEvents alias (not "Events") avoids collision with PLang.Events namespace
// EventType: v1 PLang.Events conflict — use: using app.@event; or per-file alias
// EventBinding: v1 PLang.Events conflict — use: using EventBinding = app.@event.lifecycle.binding.@this;

// Modules subsystem (action registry)
global using AppModules = app.modules.@this;

// Channels subsystem
global using AppChannels = app.channel.list.@this;
global using Channel = app.channel.@this;
global using ChannelDirection = app.channel.ChannelDirection;
global using Serializers = app.channel.serializer.list.@this;
global using SerializeOptions = app.channel.serializer.list.SerializeOptions;
global using DeserializeOptions = app.channel.serializer.list.DeserializeOptions;

// Path types (formerly FileSystem)
// Lowercase aliases — match the PLang concept names, and (unlike PascalCase
// `Path`) do not collide with System.IO.Path. Inside `namespace app.types`
// the bare name `path` is ambiguous with the child namespace `app.types.path`;
// those few files qualify with `global::app.types.path.@this`.
global using path = app.types.path.@this;
global using filepath = app.types.path.file.@this;
global using httppath = app.types.path.http.@this;

// Config subsystem
global using AppConfig = app.config.@this;
global using ConfigScope = app.config.Scope;

// Type system
global using AppTypes = app.types.@this;

// Code subsystem (the runtime escape-hatch — was Providers)
global using AppCode = app.modules.code.@this;

// Variables (was MemoryStack)
global using Variables = app.variable.list.@this;

// Snapshot subsystem
global using Snapshot = app.snapshot.@this;
global using ISnapshot = app.snapshot.ISnapshot;


// Statics — app-scoped key/value store extracted from app._statics
global using AppStatics = app.Statics.@this;

// Standalone concepts
global using ICache = app.modules.cache.ICache;
global using Debugging = app.modules.debug.@this;
global using Tester = app.tester.@this;

// Call: not a global alias — app.modules.goal.Call (the goal.call action handler) collides.
// Use app.callstack.call.@this fully qualified, or per-file alias.

// Builder: can't be global alias — v1 PLang.Builder namespace conflict (legacy).
// Inside app.@this: the `Builder` property shadows the `builder` namespace, so
// type references must be fully qualified (global::app.modules.builder.@this).
// Inside other app.*: builder.@this resolves naturally.
// Outside app.*: use app.modules.builder.@this or a per-file alias.

// App: can't be global alias — app.@this IS the app root
// Inside app.*: use app.@this (parent namespace resolves naturally)
// Outside app.*: use app.@this or per-file alias

// CallStack: can't be global alias — v1 PLang.Runtime.CallStack conflict
// Inside app.*: use callstack.@this (namespace resolves to child)
// Outside app.*: use app.callstack.@this or per-file alias

// Types WITH v1 conflicts — require per-file handling:
// Goal: use Goals.Goal.@this or per-file alias
// Visibility: use Goals.Goal.Visibility
// Action: can't global alias (System.Action conflict), use per-file alias
