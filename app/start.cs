// app is the root. A program is a list of goals; app starts them. This is the
// minimal core — the owned concepts (file, channel, type, identity, …) grow in
// from here, each as Data on the constructor.

namespace app
{
    public sealed class @this(data.@this<goal.list.@this> goal)
    {
        public data.@this<goal.list.@this> goal { get; } = goal;

        // Run the program — run all the goals. Forward to the Data; it runs the list.
        public Task<data.@this> start() => goal.start();
    }
}
