namespace app.translate;

abstract class translate {
    public abstract Task<object> to(object value, type.type target);
}

class list(IReadOnlyList<translate> items) {
    public translate for_types(type.type from, type.type to) =>
        items.First(t => t.GetType().Name == $"{from.name}_to_{to.name}");
}
