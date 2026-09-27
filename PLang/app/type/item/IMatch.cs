namespace app.type.item;

/// <summary>
/// Who a key names: the element answers itself, one of its own, or none. A type answers for its
/// name or one of its aliases (<c>"string"</c> → text). Async because answering may load what the
/// element holds (a goal's children live in its <c>.pr</c>).
/// </summary>
public interface IMatch<TSelf> where TSelf : @this
{
    System.Threading.Tasks.ValueTask<TSelf?> Match(string key);
}
