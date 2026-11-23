using System.Collections;

namespace GameVoice;

/// <summary>Thread-safe, append-only text chat for one game (room). Enumerating returns a snapshot.</summary>
public sealed class ChatLog : IReadOnlyList<ChatMessage>
{
    private readonly object _lock = new();
    private readonly List<ChatMessage> _messages = new();

    public int Count
    {
        get { lock (_lock) return _messages.Count; }
    }

    public ChatMessage this[int index]
    {
        get { lock (_lock) return _messages[index]; }
    }

    public void Add(ChatMessage message)
    {
        ArgumentNullException.ThrowIfNull(message);
        lock (_lock) _messages.Add(message);
    }

    /// <summary>Adds a user message after normalising the text. Returns false for blank text or a negative sender id.</summary>
    public bool TryAdd(int senderUserId, string? senderName, string? text)
    {
        if (senderUserId < 0) return false;
        var normalized = ChatMessage.NormalizeText(text);
        if (normalized is null) return false;

        Add(new ChatMessage(DateTime.UtcNow, senderUserId, senderName ?? string.Empty, normalized));
        return true;
    }

    public void AddSystem(string text) => Add(ChatMessage.System(text));

    public void Clear()
    {
        lock (_lock) _messages.Clear();
    }

    /// <summary>"Sender: text" lines from <paramref name="startIndex"/>, optionally without voice messages.</summary>
    public string Format(int startIndex = 0, bool hideVoice = false)
    {
        lock (_lock)
        {
            if (startIndex < 0) startIndex = 0;
            if (startIndex >= _messages.Count) return string.Empty;

            return string.Join("\n", _messages
                .Skip(startIndex)
                .Where(m => !hideVoice || !m.IsVoice)
                .Select(m => $"{m.SenderName}: {m.Text}"));
        }
    }

    public IEnumerator<ChatMessage> GetEnumerator()
    {
        ChatMessage[] snapshot;
        lock (_lock) snapshot = _messages.ToArray();
        return ((IEnumerable<ChatMessage>)snapshot).GetEnumerator();
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
