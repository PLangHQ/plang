namespace PLang.Building.Model;

public record Module(string Name, string StepNr, bool RunOnce, object Object, DateTime? Executed = null);