// The console is the process boundary — the OS entry that starts a plang
// program. It reads the entry goal's name from the command line, builds the app
// around it, and starts it. A raw string is allowed here (the perimeter) and
// nowhere below; it becomes Data<text> before it crosses into app.

namespace app.console
{
    using text = app.type.text;
    using goal = app.goal;

    public sealed class @this
    {
        // input: the goal name from the command line. No goal named → "start".
        // The result Data flows back out — Ok exits clean, Error is a non-zero exit.
        public Task<data.@this> start(data.@this<text.@this> input) =>
            new app.@this(new goal.@this { name = input ?? "start" }).start();
    }
}
