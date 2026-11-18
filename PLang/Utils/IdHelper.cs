using IdGen;

namespace PLang.Utils;

/// <summary>
/// The ids plang generates for rows (event sourced inserts, events, variables). One generator per process, so rows made in the
/// same millisecond get different sequence numbers. The generator id is picked at random when the process starts, so two plang
/// processes writing to the same database (a web service and a sync service) do not share one; two processes still get the same
/// generator id with odds of 1 in 1024. Past 4096 ids in one millisecond the generator waits for the next millisecond.
/// </summary>
public static class IdHelper
{
	private static readonly IdGenerator generator = new(
		Random.Shared.Next(0, IdGeneratorOptions.Default.IdStructure.MaxGenerators),
		new IdGeneratorOptions(sequenceOverflowStrategy: SequenceOverflowStrategy.SpinWait));

	public static long NewId() => generator.CreateId();
}
