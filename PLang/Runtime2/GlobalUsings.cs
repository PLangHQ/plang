// Entity types — global aliases for types WITHOUT v1 (Building.Model) naming conflicts
global using GoalCall = PLang.Runtime2.Engine.Goals.GoalCall;
global using GoalSteps = PLang.Runtime2.Engine.Goals.Steps.GoalSteps;
global using Step = PLang.Runtime2.Engine.Goals.Steps.Step;
global using ErrorOrder = PLang.Runtime2.Engine.Goals.Steps.ErrorOrder;
global using CacheSettings = PLang.Runtime2.Engine.Goals.Steps.CacheSettings;
global using StepCache = PLang.Runtime2.Engine.Goals.Steps.StepCache;
global using StepActions = PLang.Runtime2.Engine.Goals.Steps.Actions.StepActions;
global using IAction = PLang.Runtime2.Engine.Goals.Steps.Actions.IAction;

// Event types — no v1 conflicts
global using EngineEvents = PLang.Runtime2.Engine.Events.EngineEvents;
global using Lifecycle = PLang.Runtime2.Engine.Events.Lifecycle;
global using Bindings = PLang.Runtime2.Engine.Events.Bindings;

// Event types WITH v1 (PLang.Events) conflicts — require per-file handling:
// EventType: use "using PLang.Runtime2.Engine.Events;" or per-file alias
// EventBinding: use "using PLang.Runtime2.Engine.Events;" or per-file alias

// Standalone concepts — no v1 conflicts
global using EngineLibraries = PLang.Runtime2.Engine.Libraries.EngineLibraries;
global using Library = PLang.Runtime2.Engine.Libraries.Library;
global using ICache = PLang.Runtime2.Engine.Cache.ICache;
global using MemoryStepCache = PLang.Runtime2.Engine.Cache.MemoryStepCache;
global using StepCacheEntry = PLang.Runtime2.Engine.Cache.StepCacheEntry;
global using CallFrame = PLang.Runtime2.Engine.CallStack.CallFrame;
global using EngineDebug = PLang.Runtime2.Engine.Debug.EngineDebug;
global using EngineTesting = PLang.Runtime2.Engine.Test.EngineTesting;
global using EngineProperty = PLang.Runtime2.Engine.Properties.EngineProperty;

// Types WITH v1 conflicts — require per-file handling:
// CallStack: v1 PLang.Runtime.CallStack — use "using PLang.Runtime2.Engine.CallStack;" or per-file alias
// ExecutedStep: v1 PLang.Runtime.ExecutedStep — use "using PLang.Runtime2.Engine.CallStack;" or per-file alias
// SerializableCallStack: v1 PLang.Models.SerializableCallStack — use per-file handling

// Types WITH v1 (Building.Model) conflicts — require per-file handling:
// Goal: use "using PLang.Runtime2.Engine.Goals;" or per-file alias
// Visibility: use "using PLang.Runtime2.Engine.Goals;" or qualified reference
// ErrorHandler: use "using PLang.Runtime2.Engine.Goals.Steps;" or per-file alias
// Action: can't alias (System.Action conflict), use per-file alias
