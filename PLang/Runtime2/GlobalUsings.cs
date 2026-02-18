// Entity types — global aliases for types WITHOUT v1 (Building.Model) naming conflicts
global using GoalCall = PLang.Runtime2.Engine.Goals.GoalCall;
global using GoalSteps = PLang.Runtime2.Engine.Goals.Steps.GoalSteps;
global using Step = PLang.Runtime2.Engine.Goals.Steps.Step;
global using ErrorOrder = PLang.Runtime2.Engine.Goals.Steps.ErrorOrder;
global using CacheSettings = PLang.Runtime2.Engine.Goals.Steps.CacheSettings;
global using StepCache = PLang.Runtime2.Engine.Goals.Steps.StepCache;
global using StepActions = PLang.Runtime2.Engine.Goals.Steps.Actions.StepActions;
global using IAction = PLang.Runtime2.Engine.Goals.Steps.Actions.IAction;

// Types WITH v1 (Building.Model) conflicts — require per-file handling:
// Goal: use "using PLang.Runtime2.Engine.Goals;" or per-file alias
// Visibility: use "using PLang.Runtime2.Engine.Goals;" or qualified reference
// ErrorHandler: use "using PLang.Runtime2.Engine.Goals.Steps;" or per-file alias
// Action: can't alias (System.Action conflict), use per-file alias
