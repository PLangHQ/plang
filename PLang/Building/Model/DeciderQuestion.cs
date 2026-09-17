namespace PLang.Building.Model
{
	// One typed question put to the decision engine: what is being asked, and the finite set of
	// options it may answer with (option key -> what that option means).
	// YesNo asks the engine's noul primitive instead of choice. A noul answers with the probability
	// that the answer is yes and no separate confidence, which is the honest shape for a question
	// that has two sides: 0.2 is a confident no, not an unsure anything, and choice cannot say that.
	public record DeciderQuestion(string Instructions, Dictionary<string, string> Criteria, bool YesNo = false);

	// One answer: the option key that was chosen, how sure the engine is, and the spread over
	// every option, which the builder logs when a choice falls under the threshold.
	public record DeciderAnswer(string Choice, double Confidence, Dictionary<string, double> Probabilities);
}
