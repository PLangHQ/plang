
// Goals subsystem
global using GoalCall = app.goal.GoalCall;
global using GoalSteps = app.goal.steps.@this;
global using Step = app.goal.steps.step.@this;
global using ErrorOrder = app.goal.steps.step.ErrorOrder;
global using CacheSettings = app.goal.steps.step.CacheSettings;
global using StepActions = app.goal.steps.step.actions.@this;
global using ActionModifiers = app.goal.steps.step.actions.action.modifiers.@this;

// Event types
global using Lifecycle = app.@event.lifecycle.@this;
global using Bindings = app.@event.lifecycle.binding.list.@this;

// Event types WITH conflicts — require per-file handling:
// global::app.@event.list.@this alias (not "Events") avoids collision with PLang.Events namespace
// Trigger: v1 PLang.Events conflict — use: using app.@event; or per-file alias
// EventBinding: v1 PLang.Events conflict — use: using EventBinding = app.@event.lifecycle.binding.@this;

// Channels subsystem
global using Channel = app.channel.@this;
global using ChannelDirection = app.channel.ChannelDirection;
global using Serializers = app.channel.serializer.list.@this;
global using SerializeOptions = app.channel.serializer.list.SerializeOptions;
global using DeserializeOptions = app.channel.serializer.list.DeserializeOptions;

// Path types (formerly FileSystem)
// Lowercase aliases — match the PLang concept names, and (unlike PascalCase
// `Path`) do not collide with System.IO.Path. Inside `namespace app.type`
// the bare name `path` is ambiguous with the child namespace `app.type.path`;
// those few files qualify with `global::app.type.path.@this`.
global using path = app.type.path.@this;
global using filepath = app.type.path.file.@this;
global using httppath = app.type.path.http.@this;

// Native collection value types — collections hold Data end to end.
// Lowercase aliases match the PLang concept names. Inside `namespace app.type`
// the bare names are ambiguous with the child namespaces; those files qualify
// with `global::app.type.dict.@this` / `global::app.type.catalog.@this`.
global using dict = app.type.dict.@this;
global using Clr = app.type.clr.@this;

// Config subsystem
global using AppConfig = app.config.@this;
global using ConfigScope = app.config.Scope;

// Type system
global using AppTypes = app.type.catalog.@this;
// The type entity (moved to app.type.@this in Stage 4) was historically reached as `app.type.@this`.
// Callers that wrote `app.type.@this.X` keep working via this alias. Stage 3b/5 may sweep the call
// sites to bare `type` (collides with the contextual keyword sites, so opting for the qualified form).

// Code subsystem (the runtime escape-hatch — was Providers)
global using AppCode = app.module.code.@this;

// Variables (was MemoryStack)
global using Variables = app.variable.list.@this;

// Snapshot subsystem
global using Snapshot = app.snapshot.@this;
global using ISnapshot = app.snapshot.ISnapshot;


// Statics — app-scoped key/value store extracted from app._statics
global using AppStatics = app.Statics.@this;

// Standalone concepts
global using ICache = app.module.cache.ICache;
global using Debugging = app.module.debug.@this;
global using Test = app.test.@this;

// Call: not a global alias — app.module.goal.Call (the goal.call action handler) collides.
// Use app.callstack.call.@this fully qualified, or per-file alias.

// Builder: can't be global alias — v1 PLang.Builder namespace conflict (legacy).
// Inside app.@this: the `Builder` property shadows the `builder` namespace, so
// type references must be fully qualified (global::app.module.builder.@this).
// Inside other app.*: builder.@this resolves naturally.
// Outside app.*: use app.module.builder.@this or a per-file alias.

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
