namespace GameVoice;

/// <summary>One text chat line. <see cref="GroupId"/> and <see cref="MessageIndex"/> are only used by <see cref="LobbyChatService"/>.</summary>
public sealed record ChatMessage(DateTime Utc, int SenderUserId, string SenderName, string Text, int? GroupId = null, int MessageIndex = 0)
{
    /// <summary>Prefix of chat messages that came from speech recognition.</summary>
    public const string VoicePrefix = "[VOICE] ";

    public const string SystemSenderName = "System";

    public bool IsVoice => Text.StartsWith(VoicePrefix, StringComparison.OrdinalIgnoreCase);

    /// <summary>Normalises line endings and trims. Returns null for blank text.</summary>
    public static string? NormalizeText(string? text)
    {
        var normalized = (text ?? string.Empty).Replace("\r\n", "\n").Trim();
        return normalized.Length == 0 ? null : normalized;
    }

    public static ChatMessage System(string text) => new(DateTime.UtcNow, 0, SystemSenderName, text);
}
