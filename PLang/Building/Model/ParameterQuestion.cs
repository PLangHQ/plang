namespace PLang.Building.Model
{
	// One parameter put to the decider: what the parameter is, so the engine knows what it is
	// choosing for, and the options it may choose from (option key -> description).
	// Standalone means the Description is the whole question already, and the caller must not wrap it
	// in "Parameter 'x': ... which of these is the value of 'x'". The questions that build a
	// dictionary are of that kind: they are about one entry, and the parameter name they would be
	// wrapped with is an internal key such as variables#__passed__%result%, which reads as nonsense.
	public record ParameterQuestion(string Description, Dictionary<string, string> Candidates, bool Standalone = false, bool YesNo = false);
}
