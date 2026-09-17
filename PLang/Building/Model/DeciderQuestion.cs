namespace PLang.Building.Model
{
	// One typed question put to the decision engine: what is being asked, and the finite set of
	// options it may answer with (option key -> what that option means).
	public record DeciderQuestion(string Instructions, Dictionary<string, string> Criteria);

	// One answer: the option key that was chosen, how sure the engine is, and the spread over
	// every option, which the builder logs when a choice falls under the threshold.
	public record DeciderAnswer(string Choice, double Confidence, Dictionary<string, double> Probabilities);
}
