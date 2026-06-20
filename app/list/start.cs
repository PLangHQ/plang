namespace plang;

// The plang list — the only collection the plang layer uses. Never IReadOnlyList
// or IEnumerable in a signature; always plang.list<T>. It owns its own search.
public sealed class list<T>(IEnumerable<T> items) : IReadOnlyList<T>
{
    private readonly List<T> _items = [.. items];

    public T this[int index] => _items[index];
    public int Count => _items.Count;

    // The list searches itself — callers never reach in and filter.
    public T? first(Func<T, bool> match) => _items.FirstOrDefault(match);

    public IEnumerator<T> GetEnumerator() => _items.GetEnumerator();
    System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
}

// A thing that runs. A courier holds a Data<T> where T : IStart and calls start()
// on it — Data forwards the verb to the value, so nobody opens the Data to read
// it; the value runs itself.
public interface IStart
{
    Task<data.@this> start();
}
