using PLang.Building.Model;

namespace PLang.Exceptions
{
	/// <summary>
	/// Thrown when the goal call stack exceeds its depth limit, which in practice always means a
	/// goal is recursing without a terminating condition (directly, or through a cycle of goals).
	///
	/// Carries the goal that tripped the limit and the repeated goals at the top of the stack, so
	/// the operator sees which goal ran away rather than a bare "1000 frames".
	/// </summary>
	public class CallStackOverflowException : Exception
	{
		public CallStackOverflowException(string message, Goal? goal, int depth, string recursionChain)
			: base(message)
		{
			Goal = goal;
			Depth = depth;
			RecursionChain = recursionChain;
		}

		public Goal? Goal { get; }
		public int Depth { get; }

		/// <summary>Repeated goals at the top of the stack, most frequent first.</summary>
		public string RecursionChain { get; }
	}
}
