namespace PLang.Building.Model
{
	// One parameter put to the decider: what the parameter is, so the engine knows what it is
	// choosing for, and the options it may choose from (option key -> description).
	public record ParameterQuestion(string Description, Dictionary<string, string> Candidates);
}
