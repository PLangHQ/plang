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

	private static long reservedUpTo;

	public static long NewId()
	{
		while (true)
		{
			var id = generator.CreateId();
			if (id > Interlocked.Read(ref reservedUpTo)) return id;
		}
	}

	/// <summary>
	/// The first id of a block for many rows in one statement (insert ... select): rows get id, id + 1, ... NewId never
	/// returns an id of the block, about 4 million ids (one millisecond of this generator); it waits that millisecond out.
	/// </summary>
	public static long ReserveBlock()
	{
		var id = NewId();
		var structure = IdGeneratorOptions.Default.IdStructure;
		var end = id + (1L << (structure.GeneratorIdBits + structure.SequenceBits));
		long current;
		do { current = Interlocked.Read(ref reservedUpTo); if (current >= end) break; }
		while (Interlocked.CompareExchange(ref reservedUpTo, end, current) != current);
		return id;
	}
}
