namespace GameVoice;

/// <summary>Persists each user's preferred TTS voice (implemented by the app, e.g. over its user table).</summary>
public interface IVoicePreferenceStore
{
    Task<VoicePreference> GetAsync(int userId);

    Task<bool> SetVoiceKeyAsync(int userId, string? voiceKey);

    Task<bool> SetLanguageAsync(int userId, string? language);

    Task<bool> SetRegionAsync(int userId, string? region);

    /// <summary>Optional admin-set voice used on desktop receivers when the sender has no preference.</summary>
    Task<VoicePreference?> GetDesktopFallbackAsync(int userId) => Task.FromResult<VoicePreference?>(null);
}
