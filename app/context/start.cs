using app.type.text;

namespace app.context;

// Request-level state for a single execution. Born per run, navigated — never
// passed through method signatures. A class that needs it declares IContext and
// the runtime injects this; everything else just runs.
class context(data.@this<text.@this> id, app.app app, variable.list variable) {
    public data.@this<text.@this> id       { get; } = id;   // unique per execution
    public app.app                app      { get; } = app;  // back to the root
    public variable.list          variable { get; } = variable;  // the %variables%

    public goal.goal?      goal => app.goal.current;   // the executing goal
    public goal.step.step? step => goal?.step.current; // the executing step
}

// Capability interface: a class that needs context declares IContext.
// The runtime sets Context — the class never reaches for it, passes it, or stores
// it on a shared object. Leaf actions implement this; couriers do not.
interface IContext {
    context Context { get; set; }
}
