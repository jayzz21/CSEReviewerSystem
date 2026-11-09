namespace CseReviewer;
public class LobbyNotifier {
    private readonly object _lock = new();
    private readonly Dictionary<string, Dictionary<Guid, Func<Task>>> _subscribers = new();

    public IDisposable Subscribe(string code, Func<Task> onChanged) {
        var id = Guid.NewGuid();
        lock (_lock) {
            if (!_subscribers.TryGetValue(code, out var group)) {
                group = new Dictionary<Guid, Func<Task>>();
                _subscribers[code] = group;
            }
            group[id] = onChanged;
        }
        return new Subscription(() => Unsubscribe(code, id));
    }

    public void Notify(string code) {
        List<Func<Task>> handlers;
        lock (_lock) {
            if (!_subscribers.TryGetValue(code, out var group)) return;
            handlers = group.Values.ToList();
        }
        foreach (var handler in handlers) _ = RunHandler(handler);
    }

    private void Unsubscribe(string code, Guid id) {
        lock (_lock) {
            if (!_subscribers.TryGetValue(code, out var group)) return;
            group.Remove(id);
            if (group.Count == 0) _subscribers.Remove(code);
        }
    }

    private static async Task RunHandler(Func<Task> handler) {
        try { await handler(); }
        catch (Exception) { }
    }

    private sealed class Subscription(Action onDispose) : IDisposable {
        public void Dispose() => onDispose();
    }
}
