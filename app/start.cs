// app is the root. The console hands it one entry goal; app starts it. This is
// the minimal core — the owned concepts (channel, type, identity, …) grow in
// from here, each as Data on the constructor.

namespace app
{
    public sealed class @this(data.@this<goal.@this> goal)
    {
        public data.@this<goal.@this> goal { get; } = goal;

        // Run the program — start the entry goal. Forward to the Data; it runs
        // the goal (and the goal runs its steps). app never opens Value.
        public Task<data.@this> start() => goal.start();
    }
}
